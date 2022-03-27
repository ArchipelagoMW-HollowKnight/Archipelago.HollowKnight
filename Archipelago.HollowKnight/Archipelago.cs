using System;
using System.Collections.Generic;
using System.Linq;
using Archipelago.HollowKnight.Grants;
using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.Helpers;
using Archipelago.MultiClient.Net.Packets;
using ItemChanger;
using ItemChanger.Extensions;
using ItemChanger.Internal;
using ItemChanger.Items;
using ItemChanger.UIDefs;
using Modding;
using UnityEngine;

namespace Archipelago.HollowKnight
{
    // Known Issues
    // BUG: grubfather and (maybe) seer placements show twice in recent items display.
    // BUG: loading a save and resuming a multi doesn't work
    // BUG: any grant that gives you the void item doesn't have a sprite
    // TODO: Charm Notch rando
    // TODO: Grimmkin flame rando, I guess?
    // TODO: Test cases: Items send and receive from: Grubfather, Seer, Shops, Chests, Lore tablets, Geo Rocks, Lifeblood cocoons, Shinies, Egg Shop, Soul totems
    // TODO: NEXT: Charm cost rando
    public partial class Archipelago : Mod, ILocalSettings<ConnectionDetails>
    {
        private readonly Version ArchipelagoProtocolVersion = new Version(0, 2, 6);

        internal static Archipelago Instance;
        internal static Sprite Sprite;
        internal static Sprite SmallSprite;
        internal static System.Random Random;
        
        internal SpriteManager spriteManager;
        internal ConnectionDetails ApSettings;
        internal bool ArchipelagoEnabled = false;

        internal ArchipelagoSession session;
        private StackableItemGrants stackableItems;
        private GrubGrants grubs;
        private Dictionary<string, AbstractPlacement> vanillaItemPlacements = new();
        private long seed = 0;

        public override string GetVersion() => new Version(0, 0, 1).ToString();

        public override void Initialize(Dictionary<string, Dictionary<string, GameObject>> preloadedObjects)
        {
            base.Initialize();
            Log("Initializing");

            Instance = this;
            spriteManager = new SpriteManager(typeof(Archipelago).Assembly, "Archipelago.HollowKnight.Resources.");
            Sprite = spriteManager.GetSprite("Icon");
            SmallSprite = spriteManager.GetSprite("IconSmall");

            MenuChanger.ModeMenu.AddMode(new ArchipelagoModeMenuConstructor());

            ModHooks.SavegameLoadHook += ModHooks_SavegameLoadHook;
            Events.OnItemChangerUnhook += Events_OnItemChangerUnhook;
            ModHooks.HeroUpdateHook += ModHooks_HeroUpdateHook;

            Log("Initialized");
        }

        private void ModHooks_HeroUpdateHook()
        {
            if (!ArchipelagoEnabled)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.O))
            {
                stackableItems.GrantArcaneEgg();
            }
            else if (Input.GetKeyDown(KeyCode.G))
            {
                grubs.GrantGrub();
            }
            else if (Input.GetKeyDown(KeyCode.K))
            {
                HeroController.instance.AddGeo(1000);
            }
            else if (Input.GetKeyDown(KeyCode.L))
            {
                stackableItems.GrantRancidEgg();
            }
            else if (Input.GetKeyDown(KeyCode.Semicolon))
            {
                PlayerData.instance.IntAdd(nameof(PlayerData.dreamOrbs), 500);
                EventRegister.SendEvent("DREAM ORB COLLECT");
            }
            else if (Input.GetKeyDown(KeyCode.V))
            {
                LifebloodCocoonGrants.AwardBlueHeartsFromItemsSafely(new LifebloodItem() { amount = 4 });
                HeroController.instance.MaxHealthKeepBlue();
            }
        }

        public void ConnectAndRandomize()
        {
            if (!ArchipelagoEnabled)
            {
                return;
            }

            ItemChangerMod.CreateSettingsProfile();

            ConnectToArchipelago();
            CreateItemPlacements();
            CreateVanillaItemPlacements();
            stackableItems = new StackableItemGrants();
            grubs = new GrubGrants();
        }

        private void ConnectToArchipelago()
        {
            session = ArchipelagoSessionFactory.CreateSession(ApSettings.ServerUrl, ApSettings.ServerPort);
            session.Items.ItemReceived += Items_ItemReceived;

            var loginResult = session.TryConnectAndLogin("Hollow Knight", ApSettings.SlotName, ArchipelagoProtocolVersion, ItemsHandlingFlags.AllItems, password: ApSettings.ServerPassword);

            if (loginResult is LoginFailure failure)
            {
                // TODO: Better error handling to come later.
                throw new Exception(string.Join(", ", failure.Errors));
            }
            else if (loginResult is LoginSuccessful success)
            {                    
                seed = (long)success.SlotData["seed"];
                Random = new System.Random(Convert.ToInt32(seed));

                SpecialPlacementHandler.Random = Random;
                SpecialPlacementHandler.GrubFatherCosts = SlotDataExtract.ExtractObjectFromSlotData<Dictionary<string, int>>(success.SlotData["grub_costs"]);
                SpecialPlacementHandler.SeerCosts = SlotDataExtract.ExtractObjectFromSlotData<Dictionary<string, int>>(success.SlotData["essence_costs"]);
            }
        }

        private void Items_ItemReceived(ReceivedItemsHelper helper)
        {
            var itemReceived = helper.DequeueItem();
            ReceiveItem(itemReceived.Item);
        }

        public void ReceiveItem(int id)
        {
            LogDebug($"Receiving item ID {id}");
            var name = session.Items.GetItemName(id);
            LogDebug($"Item name is {name}.");

            // TODO: implement essence and egg shops (possibly by auto granting location check with enough essence/eggs collected)
            // TODO: buying an item in the shop should send a location check (test it now, see if it works)
            // TODO: create an interface and genericize the xxxGrants classes.
            if (vanillaItemPlacements.TryGetValue(name, out var placement))
            {
                LogDebug($"Found vanilla placement for {name}.");

                if (StackableItemGrants.IsStackableItem(name))
                {
                    LogDebug($"Detected stackable item received. Granting a: {name}");
                    stackableItems.GrantItemByName(name);
                    return;
                }

                if (GeoRockGrants.IsGeoRockItem(name))
                {
                    LogDebug("Detecting vanilla item is geo rock item.");
                    GeoRockGrants.RewardGeoFromItemsSafely(placement.Items);
                    return;
                }

                if (LifebloodCocoonGrants.IsLifebloodCocoonItem(name))
                {
                    LogDebug("Detecting vanilla item is lifeblood cocoon.");
                    LifebloodCocoonGrants.AwardBlueHeartsFromItemsSafely(placement.Items.ToArray());
                    return;
                }

                if (name == "Grub")
                {
                    LogDebug("Detecting vanilla item is a grub.");
                    grubs.GrantGrub();
                    return;
                }

                placement.GiveAll(new GiveInfo()
                {
                    FlingType = FlingType.DirectDeposit,
                    Container = Container.Unknown,
                    MessageType = MessageType.Corner
                });
            }
            else
            {
                LogDebug($"Could not find vanilla placement for {name}.");
            }
        }

        private void CreateItemPlacements()
        {
            void ScoutCallback(LocationInfoPacket packet)
            {
                foreach (var item in packet.Locations)
                {
                    var locationName = session.Locations.GetLocationNameFromId(item.Location);
                    var itemName = session.Items.GetItemName(item.Item);

                    PlaceItem(locationName, itemName, item.Item);
                }
            }

            var locations = new List<long>(session.Locations.AllLocations);
            session.Locations.ScoutLocationsAsync(ScoutCallback, locations.ToArray());
        }

        public void PlaceItem(string location, string name, int apLocationId)
        {
            LogDebug($"Placing item {name} into {location} with ID {apLocationId}");
            var originalLocation = string.Copy(location);
            location = StripShopSuffix(location);
            AbstractLocation loc = Finder.GetLocation(location);
            // TODO: remove this when logic has properly been imported and this mod can handle all location names.
            if (loc == null)
            {
                LogDebug($"Location was null: Name: {location}.");
                return;
            }

            AbstractPlacement pmt = loc.Wrap();
            AbstractItem item;

            
            if (Finder.ItemNames.Contains(name))
            {
                // Since HK is a remote items game, I don't want the placement to actually do anything. The item will come from the server.
                var originalItem = Finder.GetItem(name);
                if (originalItem != null)
                {
                    item = new DisguisedVoidItem(originalItem);
                }
                else
                {
                    item = new VoidItem();
                }
            }
            else
            {
                // If item doesn't belong to Hollow Knight, then it is a remote item for another game.
                item = new ArchipelagoItem(name);
            }

            item.OnGive += (x) =>
            {
                var id = session.Locations.GetLocationIdFromName("Hollow Knight", originalLocation);
                session.Locations.CompleteLocationChecks(id);
            };

            if (SpecialPlacementHandler.IsShopPlacement(location))
            {
                LogDebug($"Detected shop placement for location: {location}");
                SpecialPlacementHandler.PlaceShopItem(pmt, item);
            }
            else if (SpecialPlacementHandler.IsSeerPlacement(location))
            {
                LogDebug($"Detected seer placement for location: {location}.");
                SpecialPlacementHandler.PlaceSeerItem(originalLocation, pmt, item);
            }
            else if (SpecialPlacementHandler.IsEggShopPlacement(location))
            {
                LogDebug($"Detected egg shop placement for location: {location}.");
                SpecialPlacementHandler.PlaceEggShopItem(pmt, item);
            }
            else if (SpecialPlacementHandler.IsGrubfatherPlacement(location))
            {
                LogDebug($"Detected Grubfather placement for original location: {originalLocation}. Trimmed location: {location}");
                SpecialPlacementHandler.PlaceGrubfatherItem(originalLocation, pmt, item);
            }
            else
            {
                pmt.Add(item);
            }

            ItemChangerMod.AddPlacements(pmt.Yield());
        }

        private string StripShopSuffix(string location)
        {
            if (string.IsNullOrEmpty(location))
            {
                return null;
            }

            var names = new[] 
            { 
                LocationNames.Sly, LocationNames.Sly_Key, LocationNames.Iselda, LocationNames.Salubra, 
                LocationNames.Leg_Eater, LocationNames.Egg_Shop, LocationNames.Seer, LocationNames.Grubfather 
            };

            foreach (var name in names)
            {
                if (location.StartsWith(name))
                {
                    return location.Substring(0, name.Length);
                }
            }
            return location;
        }

        private void CreateVanillaItemPlacements()
        {
            var allItems = Finder.GetFullItemList().Where(kvp => kvp.Value is not CustomSkillItem).ToDictionary(x => x.Key, x => x.Value);
            foreach (var kvp in allItems)
            {
                LogDebug($"Creating ArchipelagoLocation for a vanilla placement: Name: {kvp.Key}, Item: {kvp.Value}");
                var name = kvp.Key;
                var item = kvp.Value;

                var apLocation = new ArchipelagoLocation("Vanilla_"+name);
                var placement = apLocation.Wrap();
                placement.Add(item);
                item.UIDef = new MsgUIDef()
                {
                    name = new BoxedString(item.UIDef.GetPreviewName()),
                    shopDesc = new BoxedString(item.UIDef.GetShopDesc()),
                    sprite = new BoxedSprite(item.UIDef.GetSprite())
                };

                vanillaItemPlacements.Add(name, placement);
            }

            ItemChangerMod.AddPlacements(vanillaItemPlacements.Values.ToList());
        }

        private void ModHooks_SavegameLoadHook(int obj)
        {
            if (ApSettings == default)
            {
                return;
            }

            ConnectToArchipelago();
            vanillaItemPlacements = RetrieveVanillaItemPlacementsFromSave();
        }

        //TODO: I don't think this works. I need to retireve the custom placements somehow. homothety suggested ItemChanger.Internal.Ref.Settings.Placements
        private Dictionary<string, AbstractPlacement> RetrieveVanillaItemPlacementsFromSave()
        {
            var placements = new Dictionary<string, AbstractPlacement>();
            var allItems = Finder.GetFullItemList().Where(kvp => kvp.Value is not CustomSkillItem).Select(x => x.Key);
            foreach (var item in allItems)
            {
                var location = Finder.GetLocation($"Vanilla_{item}");
                if (location == null)
                {
                    LogDebug($"Could not find previous vanilla item placement for item name: {item}");
                    continue;
                }
                placements.Add(item, location.Wrap());
            }
            return placements;
        }

        private void Events_OnItemChangerUnhook()
        {
            DisconnectArchipelago();
            vanillaItemPlacements = null;
            stackableItems = null;
            SpecialPlacementHandler.SeerCosts = null;
            SpecialPlacementHandler.GrubFatherCosts = null;
        }

        private void DisconnectArchipelago()
        {
            if (session?.Socket != null && session.Socket.Connected)
            {
                session.Socket.DisconnectAsync();
            }

            session = null;
        }

        public void OnLoadLocal(ConnectionDetails details)
        {
            ApSettings = details;
        }

        public ConnectionDetails OnSaveLocal()
        {
            return ApSettings;
        }
    }
}