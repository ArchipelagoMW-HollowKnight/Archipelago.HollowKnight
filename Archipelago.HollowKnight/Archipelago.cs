using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.Helpers;
using Archipelago.MultiClient.Net.Packets;
using ItemChanger;
using ItemChanger.Extensions;
using ItemChanger.Internal;
using ItemChanger.Items;
using ItemChanger.Placements;
using ItemChanger.Tags;
using ItemChanger.UIDefs;
using Modding;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Archipelago.HollowKnight
{
    public partial class Archipelago : Mod, ILocalSettings<ConnectionDetails>
    {
        private readonly Version ArchipelagoProtocolVersion = new Version(0, 2, 6);

        internal static Archipelago Instance;
        internal static Sprite Sprite;
        internal static System.Random Random;
        
        internal SpriteManager spriteManager;
        internal ConnectionDetails ApSettings;
        internal bool ArchipelagoEnabled = false;

        internal ArchipelagoSession session;
        private StackableItemGrants stackableItems;
        private Dictionary<string, AbstractPlacement> vanillaItemPlacements = new();
        private long seed = 0;
        private Dictionary<string, int> grubFatherCosts = new();

        public override string GetVersion() => new Version(0, 0, 1).ToString();

        public override void Initialize(Dictionary<string, Dictionary<string, GameObject>> preloadedObjects)
        {
            base.Initialize();
            Log("Initializing");

            Instance = this;
            spriteManager = new SpriteManager(typeof(Archipelago).Assembly, "Archipelago.HollowKnight.Resources.");
            Sprite = spriteManager.GetSprite("Icon");

            MenuChanger.ModeMenu.AddMode(new ArchipelagoModeMenuConstructor());

            ModHooks.SavegameLoadHook += ModHooks_SavegameLoadHook;
            On.UIManager.StartNewGame += UIManager_StartNewGame;
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
        }

        private void UIManager_StartNewGame(On.UIManager.orig_StartNewGame orig, UIManager self, bool permaDeath, bool bossRush)
        {
            if (!ArchipelagoEnabled)
            {
                orig(self, permaDeath, bossRush);
                return;
            }

            ItemChangerMod.CreateSettingsProfile();

            ConnectToArchipelago();
            CreateItemPlacements();
            CreateVanillaItemPlacements();
            stackableItems = new StackableItemGrants();
            orig(self, permaDeath, bossRush);
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
                try
                {
                    LogDebug($"Seed object type is: {success.SlotData["seed"].GetType().FullName}");
                    seed = (long)success.SlotData["seed"];
                    Random = new System.Random(Convert.ToInt32(seed));
                    LogDebug("Successfully read the seed from the slot data.");
                }
                catch (Exception ex)
                {
                    LogError($"Could not get seed out of slot data or seed was invalid format. Message: {ex.Message}");
                    Random = new System.Random();
                }

                grubFatherCosts = ExtractGrubfatherCosts(success.SlotData["grub_costs"]);
            }
        }

        private Dictionary<string, int> ExtractGrubfatherCosts(object v)
        {
            var jobj = v as JObject;
            var costsDict = jobj?.ToObject<Dictionary<string, int>>();
            if (costsDict == null)
            {
                LogError("Could not read grubfather costs. Defaulting to random costs.");
                return new Dictionary<string, int>
                {
                    ["Grubfather_1"] = Random.Next(0, 47),
                    ["Grubfather_2"] = Random.Next(0, 47),
                    ["Grubfather_3"] = Random.Next(0, 47),
                    ["Grubfather_4"] = Random.Next(0, 47),
                    ["Grubfather_5"] = Random.Next(0, 47),
                    ["Grubfather_6"] = Random.Next(0, 47),
                    ["Grubfather_7"] = Random.Next(0, 47),
                };
            }
            LogDebug("Successfully read grubfather cost.");
            return costsDict;
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

            //if (IsShopPlacement(name))
            //{
            //    var shopLocation = Finder.GetLocation(StripShopSuffix(name));
            //    var shopPlacement = shopLocation.Wrap() as ShopPlacement;
            //}

            // TODO: receive item doesn't seem to work
            // TODO: implement essence and egg shops
            // TODO: receiving items that belonged to shop slots doesn't work
            if (StackableItemGrants.IsStackableItem(name))
            {
                LogDebug($"Detected stackable item received. Granting a: {name}");
                stackableItems.GrantItemByName(name);
                return;
            }

            if (vanillaItemPlacements.TryGetValue(name, out var placement))
            {
                LogDebug($"Found vanilla placement for {name}.");
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
            location = StripShopSuffix(location);
            AbstractLocation loc = Finder.GetLocation(location);
            // TODO: remove this when logic has properly been imported and AP data isn't corrupt.
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
                item = new VoidItem();
            }
            else
            {
                // If item doesn't belong to Hollow Knight, then it is a remote item for another game.
                item = new ArchipelagoItem(name);
            }

            pmt.OnVisitStateChanged += (x) =>
            {
                LogDebug($"State visit changed. Old flags: {x.Orig} New flags: {x.NewFlags}");
                if (!x.NoChange && x.NewFlags.HasFlag(VisitState.ObtainedAnyItem))
                {
                    session.Locations.CompleteLocationChecks(apLocationId);
                }
            };

            if (IsShopPlacement(location))
            {
                var shopPlacement = pmt as ShopPlacement;
                shopPlacement.AddItemWithCost(item, GenerateGeoCost());
            }
            else if (location.StartsWith("Grubfather"))
            {
                var costChestPlacement = pmt as CostChestPlacement;
                if (!costChestPlacement.HasTag<DestroyGrubRewardTag>())
                { 
                    var tag = costChestPlacement.AddTag<DestroyGrubRewardTag>();
                    tag.destroyRewards = GrubfatherRewards.AllNonGeo;
                }
                costChestPlacement.AddItem(item, Cost.NewGrubCost(GetGrubCostForLocation(location)));
            }
            else
            {
                pmt.Add(item);
            }

            ItemChangerMod.AddPlacements(pmt.Yield());
        }

        private int GetGrubCostForLocation(string location)
        {
            if (grubFatherCosts.TryGetValue(location, out var cost))
            {
                return cost;
            }
            else
            {
                LogError($"Attempted to get Grubfather cost for location '{location}' but key was not present.");
                return 0;
            }
        }

        private Cost GenerateGeoCost()
        {
            return Cost.NewGeoCost(Random.Next(1, 400));
        }

        private bool IsShopPlacement(string location)
        {
            var names = new[] { LocationNames.Sly, LocationNames.Sly_Key, LocationNames.Iselda, LocationNames.Salubra, LocationNames.Leg_Eater };
            return names.Any(x => location.StartsWith(x));
        }

        private string StripShopSuffix(string location)
        {
            if (string.IsNullOrEmpty(location))
            {
                return null;
            }

            var names = new[] { LocationNames.Sly, LocationNames.Sly_Key, LocationNames.Iselda, LocationNames.Salubra, LocationNames.Leg_Eater };
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
                //InteropTag tag = placement.AddTag<InteropTag>();
                //tag.Message = "RecentItems";
                //tag.Properties["DisplaySource"] = name;

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
        }

        private void Events_OnItemChangerUnhook()
        {
            DisconnectArchipelago();
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