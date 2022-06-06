using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Archipelago.HollowKnight.IC;
using Archipelago.HollowKnight.MC;
using Archipelago.HollowKnight.Placements;
using Archipelago.HollowKnight.SlotData;
using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.Models;
using Archipelago.MultiClient.Net.Packets;
using ItemChanger;
using ItemChanger.Extensions;
using ItemChanger.Internal;
using ItemChanger.Items;
using ItemChanger.Tags;
using ItemChanger.UIDefs;
using Modding;
using RecentItemsDisplay;
using UnityEngine;

namespace Archipelago.HollowKnight
{
    public class Archipelago : Mod, IGlobalSettings<ConnectionDetails>, ILocalSettings<ConnectionDetails>
    {
        private readonly Version ArchipelagoProtocolVersion = new Version(0, 3, 0);

        public static Archipelago Instance;
        public SlotOptions SlotOptions { get; set; }
        public bool ArchipelagoEnabled { get; set; }
        public Dictionary<string, int> GrubfatherCosts { get; private set; }
        public Dictionary<string, int> SeerCosts { get; private set; }
        public Dictionary<string, int> EggCosts { get; private set; }
        public Dictionary<string, int> SalubraCharmCosts { get; private set; }
        public Dictionary<string, List<long>> LocationIDsByLocation { get; private set; }

        public List<int> NotchCosts { get; private set; }

        internal static Sprite Sprite;
        internal static Sprite SmallSprite;
        internal static System.Random Random;
        internal static FieldInfo obtainStateFieldInfo;

        internal SpriteManager spriteManager;
        internal ConnectionDetails ApSettings;
        internal ArchipelagoSession session;

        private Dictionary<string, AbstractPlacement> vanillaItemPlacements = new();
        private long seed = 0;
        private int slot;
        private TimeSpan timeBetweenReceiveItem = TimeSpan.FromMilliseconds(500);
        private DateTime lastUpdate = DateTime.MinValue;
        private List<IPlacementHandler> placementHandlers;
        private Goal goal = null;

        public override string GetVersion() => new Version(0, 0, 2).ToString();

        public override void Initialize(Dictionary<string, Dictionary<string, GameObject>> preloadedObjects)
        {
            base.Initialize();
            Log("Initializing");
            Instance = this;
            spriteManager = new SpriteManager(typeof(Archipelago).Assembly, "Archipelago.HollowKnight.Resources.");
            Sprite = spriteManager.GetSprite("Icon");
            SmallSprite = spriteManager.GetSprite("IconSmall");
            obtainStateFieldInfo = typeof(AbstractItem).GetField("obtainState", BindingFlags.NonPublic | BindingFlags.Instance);

            MenuChanger.ModeMenu.AddMode(new ArchipelagoModeMenuConstructor());

            ModHooks.SavegameLoadHook += ModHooks_SavegameLoadHook;
            ItemChanger.Events.OnItemChangerUnhook += Events_OnItemChangerUnhook;
            ModHooks.HeroUpdateHook += ModHooks_HeroUpdateHook;

            Log("Initialized");
        }

        public void DeclareVictory()
        {
            Archipelago.Instance.LogDebug($"Declaring victory if ArchipelagEnabled.  ArchipelagoEnabled = {ArchipelagoEnabled}");
            if (ArchipelagoEnabled)
            {
                session.Socket.SendPacket(new StatusUpdatePacket()
                {
                    Status = ArchipelagoClientState.ClientGoal
                });
            }
        }

        private void ModHooks_HeroUpdateHook()
        {
            if (!ArchipelagoEnabled)
            {
                return;
            }

            if (DateTime.Now - timeBetweenReceiveItem > lastUpdate && session.Items.Any())
            {
                LogDebug($"Item Index from lib is: {session.Items.Index}. From APSettings it is: {ApSettings.ItemIndex}");
                if (ApSettings.ItemIndex >= session.Items.Index)
                {
                    session.Items.DequeueItem();
                }
                else
                {
                    ReceiveItem(session.Items.DequeueItem());
                    ApSettings.ItemIndex++;
                }
            }
        }

        public void ConnectAndRandomize()
        {
            if (!ArchipelagoEnabled)
            {
                return;
            }

            LocationIDsByLocation = new();
            ItemChangerMod.CreateSettingsProfile();

            ConnectToArchipelago();

            goal = Goal.GetGoal(SlotOptions.Goal);
            if (goal == null)
            {
                LogError($"Listed goal is {SlotOptions.Goal}, which is greater than {Goals.MAX}.  Is this an outdated client?");
                throw new Exception("Unrecognized goal condition (are you running an outdated client?)");
            }
            goal.Select();

            if (SlotOptions.RandomCharmCosts != -1)
            {
                RandomizeCharmCosts();
            }
            CreateItemPlacements();
            CreateVanillaItemPlacements();
        }

        private void RandomizeCharmCosts()
        {
            ItemChangerMod.Modules.Add<ItemChanger.Modules.NotchCostUI>();
            ItemChangerMod.Modules.Add<ItemChanger.Modules.ZeroCostCharmEquip>();
            var playerDataEditModule = ItemChangerMod.Modules.GetOrAdd<ItemChanger.Modules.PlayerDataEditModule>();
            LogDebug(playerDataEditModule);
            for (int i = 0; i < NotchCosts.Count; i++)
            {
                playerDataEditModule.AddPDEdit($"charmCost_{i + 1}", NotchCosts[i]);
            }
        }

        private void ConnectToArchipelago()
        {
            session = ArchipelagoSessionFactory.CreateSession(ApSettings.ServerUrl, ApSettings.ServerPort);

            var loginResult = session.TryConnectAndLogin("Hollow Knight", ApSettings.SlotName, ArchipelagoProtocolVersion, ItemsHandlingFlags.AllItems, password: ApSettings.ServerPassword);

            if (loginResult is LoginFailure failure)
            {
                var errors = string.Join(", ", failure.Errors);
                LogError($"Unable to connect to Archipelago because: {string.Join(", ", failure.Errors)}");
                throw new ArchipelagoConnectionException(errors);
            }
            else if (loginResult is LoginSuccessful success)
            {
                // Read slot data.
                seed = (long)success.SlotData["seed"];
                slot = success.Slot;
                Random = new System.Random(Convert.ToInt32(seed));

                Costs.Random = Random;
                GrubfatherCosts = SlotDataExtract.ExtractObjectFromSlotData<Dictionary<string, int>>(success.SlotData["Grub_costs"]);
                SeerCosts = SlotDataExtract.ExtractObjectFromSlotData<Dictionary<string, int>>(success.SlotData["Essence_costs"]);
                EggCosts = SlotDataExtract.ExtractObjectFromSlotData<Dictionary<string, int>>(success.SlotData["Egg_costs"]);
                SalubraCharmCosts = SlotDataExtract.ExtractObjectFromSlotData<Dictionary<string, int>>(success.SlotData["Charm_costs"]);
                NotchCosts = SlotDataExtract.ExtractArrayFromSlotData<List<int>>(success.SlotData["notch_costs"]);
                SlotOptions = SlotDataExtract.ExtractObjectFromSlotData<SlotOptions>(success.SlotData["options"]);

                InitializePlacementHandlers();
            }
        }

        private void InitializePlacementHandlers()
        {
            placementHandlers = new List<IPlacementHandler>()
            {
                new ShopPlacementHandler(),
                new GrubfatherPlacementHandler(GrubfatherCosts),
                new SeerPlacementHandler(SeerCosts),
                new EggShopPlacementHandler(EggCosts),
                new SalubraCharmShopPlacementHandler(SalubraCharmCosts)
            };
        }

        public void ReceiveItem(NetworkItem item)
        {
            LogDebug($"Receiving item ID {item.Item}");
            var name = session.Items.GetItemName(item.Item);
            LogDebug($"Item name is {name}.");

            if (vanillaItemPlacements.TryGetValue(name, out var placement))
            {
                LogDebug($"Found vanilla placement for {name}.");

                var uiName = placement.GetUIName();
                var sprite = placement.Items.FirstOrDefault()?.UIDef.GetSprite();
                if (item.Player == slot)
                {
                    ItemDisplayMethods.ShowItem(new ItemDisplayArgs(uiName, string.Empty, sprite)
                    {
                        DisplayMessage = $"{uiName}\nreceived from yourself."
                    });
                }
                else
                {
                    var playerName = session.Players.GetPlayerName(item.Player);
                    ItemDisplayMethods.ShowItem(new ItemDisplayArgs(uiName, string.Empty, sprite)
                    {
                        DisplayMessage = $"{uiName}\nreceived from {playerName}."
                    });
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
                MenuChanger.ThreadSupport.BeginInvoke(() =>
                {
                    foreach (var item in packet.Locations)
                    {
                        var locationName = session.Locations.GetLocationNameFromId(item.Location);
                        var itemName = session.Items.GetItemName(item.Item);

                        PlaceItem(locationName, itemName, item);
                    }
                });
            }

            var locations = new List<long>(session.Locations.AllLocations);
            session.Locations.ScoutLocationsAsync(ScoutCallback, locations.ToArray());
        }

        internal void SetItemTags(AbstractItem item, string name, string slotName)
        {
            var tag = item.AddTag<InteropTag>();
            tag.Message = "RecentItems";
            tag.Properties["DisplayMessage"] = $"{name}\nsent to {slotName}.";
        }

        public void PlaceItem(string location, string name, NetworkItem netItem)
        {
            LogDebug($"[PlaceItem] Placing item {name} into {location} with ID {netItem.Item}");
            
            var originalLocation = string.Copy(location);
            
            location = StripShopSuffix(location);
            AbstractLocation loc = Finder.GetLocation(location);
            
            string targetSlotName = null;
            if (netItem.Player != slot)
            {
                targetSlotName = session.Players.GetPlayerName(netItem.Player);
            }

            if (loc == null)
            {
                LogDebug($"[PlaceItem] Location was null: Name: {location}.");
                return;
            }

            AbstractPlacement pmt = loc.Wrap();
            AbstractItem item;

            if (Finder.ItemNames.Contains(name))
            {
                // Since HK is a remote items game, I don't want the placement to actually do anything. The item will come from the server.
                var originalItem = Finder.GetItem(name);
                item = new DisguisedVoidItem(originalItem, targetSlotName);

                if (netItem.Player == slot)
                {
                    item.ModifyItem += (x) =>
                    {
                        try
                        {
                            x.Info.MessageType = MessageType.None;
                        }
                        catch { }
                    };

                    var tag = item.AddTag<InteropTag>();
                    tag.Message = "RecentItems";
                    tag.Properties["IgnoreItem"] = true;
                }
                else
                {
                    SetItemTags(item, originalItem.GetPreviewName(), targetSlotName);
                }
            }
            else
            {
                // If item doesn't belong to Hollow Knight, then it is a remote item for another game.
                item = new ArchipelagoItem(name, targetSlotName, netItem.Flags);
                SetItemTags(item, name, targetSlotName);
            }

            item.OnGive += (x) =>
            {
                var id = session.Locations.GetLocationIdFromName("Hollow Knight", originalLocation);
                session.Locations.CompleteLocationChecks(id);
            };

            bool handled = false;
            foreach (var handler in placementHandlers)
            {
                if (handler.CanHandlePlacement(originalLocation))
                {
                    handler.HandlePlacement(pmt, item, originalLocation);
                    handled = true;
                    break;
                }
            }

            if (!handled)
            {
                pmt.Add(item);
            }
            if (LocationIDsByLocation.TryGetValue(location, out List<long> ids))
            {
                ids.Add(netItem.Location);
            }
            else
            {
                LocationIDsByLocation.Add(location, new List<long> { netItem.Location });
            }
            // I'm not certain why this override has to be added *every* time, rather than just the first time for each location.
            // But shops and other multi-item checks don't work otherwise.
            pmt.OnVisitStateChanged += (VisitStateChangedEventArgs obj) =>
            {
                if (obj.NewFlags.HasFlag(VisitState.Previewed) && !obj.Orig.HasFlag(VisitState.Previewed))
                {
                    session.Socket.SendPacketAsync(new LocationScoutsPacket()
                    {
                        CreateAsHint = true,
                        Locations = LocationIDsByLocation[location].ToArray()
                    });
                };
            };
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
                LocationNames.Sly_Key, LocationNames.Sly, LocationNames.Iselda, LocationNames.Salubra,
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

                var apLocation = new ArchipelagoLocation("Vanilla_" + name);
                var placement = apLocation.Wrap();
                placement.Add(item);

                try
                {
                    item.UIDef = new MsgUIDef()
                    {
                        name = new BoxedString(item.UIDef.GetPreviewName()),
                        shopDesc = new BoxedString(item.UIDef.GetShopDesc()),
                        sprite = new BoxedSprite(item.UIDef.GetSprite())
                    };
                }
                catch (Exception ex)
                {
                    item.UIDef = new MsgUIDef()
                    {
                        name = new BoxedString(item.UIDef.GetPreviewName()),
                        shopDesc = new BoxedString(item.UIDef.GetShopDesc()),
                        sprite = new EmptySprite()
                    };
                }
                var tag = item.AddTag<InteropTag>();
                tag.Message = "RecentItems";
                tag.Properties["IgnoreItem"] = true;

                item.OnGive += (x) =>
                {
                    try
                    {
                        obtainStateFieldInfo.SetValue(x.Item, ObtainState.Unobtained);
                    }
                    catch (Exception ex)
                    {
                        LogError("Failure in OnGive() on a vanilla placement.");
                        LogError(ex);
                    }
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

        //TODO: I don't think this works. I need to retrieve the custom placements somehow. homothety suggested ItemChanger.Internal.Ref.Settings.Placements
        /* When loading an existing game:
         *      - Load my vanilla placements, this could be done with a ItemChanger Tag - would have their own Tag type
         *      - Load my DisguisedVoidItem placements, this could be done with tag (or override OnLoad)
         *      - Load my ArchipelagoItem placements, which could probably be done with the same tag as DisguisedVoidItem
        */
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
        }

        public void DisconnectArchipelago()
        {
            slot = 0;
            seed = 0;
            vanillaItemPlacements = null;
            placementHandlers = null;

            if (session?.Socket != null && session.Socket.Connected)
            {
                session.Socket.DisconnectAsync();
            }

            session = null;
        }

        public void OnLoadLocal(ConnectionDetails details)
        {
            if(details.SlotName == null || details.SlotName == "")  // Apparently, this is called even before a save is loaded.  Catch this.
            {
                return;
            }
            ApSettings = details;
        }

        public ConnectionDetails OnSaveLocal()
        {
            return ApSettings;
        }

        public void OnLoadGlobal(ConnectionDetails details)
        {
            ApSettings = details;
            ApSettings.ItemIndex = 0;
        }

        public ConnectionDetails OnSaveGlobal()
        {
            return new ConnectionDetails()
            {
                ServerUrl = ApSettings.ServerUrl,
                ServerPort = ApSettings.ServerPort,
                SlotName = ApSettings.SlotName
            };
        }
    }
}