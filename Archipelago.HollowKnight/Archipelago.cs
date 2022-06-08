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
        /// <summary>
        /// Archipelago Protocol Version
        /// </summary>
        private readonly Version ArchipelagoProtocolVersion = new Version(0, 3, 2);
        /// <summary>
        /// Mod version as reported to the modding API
        /// </summary>
        public override string GetVersion() => new Version(0, 0, 3).ToString();
        public static Archipelago Instance;
        public SlotOptions SlotOptions { get; set; }
        public bool ArchipelagoEnabled { get; set; }

        /// <summary>
        /// Costs of Grubfather shop items (in Grubs) as stored in slot data.
        /// </summary>
        public Dictionary<string, int> GrubfatherCosts { get; private set; }
        /// <summary>
        /// Costs of Seer shop items (in Essence) as stored in slot data.
        /// </summary>
        public Dictionary<string, int> SeerCosts { get; private set; }
        /// <summary>
        /// Costs of Egg Shop items (in Rancid Eggs) as stored in slot data.
        /// </summary>
        public Dictionary<string, int> EggCosts { get; private set; }
        /// <summary>
        /// Costs of Salubra shop items (in required charms) as stored in slot data.
        /// </summary>
        public Dictionary<string, int> SalubraCharmCosts { get; private set; }
        /// <summary>
        /// Our slot number.
        /// </summary>
        public int Slot { get => slot; }
        /// <summary>
        /// Randomized charm notch costs as stored in slot data.
        /// </summary>
        public List<int> NotchCosts { get; private set; }

        internal static Sprite Sprite;
        internal static Sprite SmallSprite;
        internal static System.Random Random;
        internal static FieldInfo obtainStateFieldInfo;

        internal SpriteManager spriteManager;
        internal ConnectionDetails ApSettings;
        internal ArchipelagoSession session;

        /// <summary>
        /// Allows lookup of a placement by its location ID number.  Used during syncing and shared-slot coop.
        /// </summary>
        internal Dictionary<long, AbstractPlacement> placementsByLocationID = new();

        /// <summary>
        /// List of pending locations.
        /// </summary>
        private HashSet<long> deferredLocationChecks = new();
        private bool deferringLocationChecks = false;

        private long seed = 0;
        private int slot;
        private TimeSpan timeBetweenReceiveItem = TimeSpan.FromMilliseconds(500);
        private DateTime lastUpdate = DateTime.MinValue;
        private List<IPlacementHandler> placementHandlers;
        private Goal goal = null;

        /// <summary>
        /// A preset GiveInfo structure that avoids creating geo and places messages in the corner.
        /// </summary>
        internal GiveInfo RemoteGiveInfo = new()
        {
            FlingType = FlingType.DirectDeposit,
            Callback = null,
            Container = Container.Unknown,
            MessageType = MessageType.Corner
        };

        /// <summary>
        /// A preset GiveInfo structure that avoids creating geo and outputs no messages, e.g. for Start Items.
        /// </summary>
        internal GiveInfo SilentGiveInfo = new()
        {
            FlingType = FlingType.DirectDeposit,
            Callback = null,
            Container = Container.Unknown,
            MessageType = MessageType.None
        };


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
            On.HeroController.Start += HeroController_Start;

            Log("Initialized");
        }

        private void HeroController_Start(On.HeroController.orig_Start orig, HeroController self)
        {
            orig(self);
            // self.AddGeo(0);  // See if we can force the geo counter to appear immediately.
            // self.AddGeoToCounter(0);
            // self.geoCounter.AddGeo(0);
            SynchronizeCheckedLocations();
            StopDeferringLocationChecks();
        }

        private void SynchronizeCheckedLocations()
        {
            if (ArchipelagoEnabled)
            {
                DeferLocationChecks();
                while (ReceiveNextItem());  // Receive items until the queue is empty.

                foreach(long location in session.Locations.AllLocationsChecked)
                {
                    LogDebug($"Marking location {location} as checked in-game.");
                    MarkLocationAsChecked(location);
                }
            }
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

        private bool ReceiveNextItem()
        {
            if (!session.Items.Any())
            {
                return false;  // No items are waiting.
            }
            LogDebug($"Item Index from lib is: {session.Items.Index}. From APSettings it is: {ApSettings.ItemIndex}");
            NetworkItem netItem = session.Items.DequeueItem();  // Read the next item
            if (ApSettings.ItemIndex >= session.Items.Index)  // We've already handled this, so be done
            {
                return true;
            }
            try
            {
                ReceiveItem(netItem);
            }
            finally
            {
                ApSettings.ItemIndex++;
            }
            return true;
        }

        private void ModHooks_HeroUpdateHook()
        {
            if (!ArchipelagoEnabled)
            {
                return;
            }
            if(deferringLocationChecks)
            {
                StopDeferringLocationChecks();
            }

            if (DateTime.Now - timeBetweenReceiveItem > lastUpdate && session.Items.Any())
            {
                ReceiveNextItem();
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

            try
            {
                goal = Goal.GetGoal(SlotOptions.Goal);
            }
            catch (ArgumentOutOfRangeException ex)
            {
                LogError($"Listed goal is {SlotOptions.Goal}, which is greater than {GoalsLookup.MAX}.  Is this an outdated client?");
                throw ex;
            }
            goal.Select();

            ApSettings.ItemIndex = 0;
            if (SlotOptions.RandomCharmCosts != -1)
            {
                RandomizeCharmCosts();
            }
            InitializePlacementHandlers();
            CreateItemPlacements();
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

        public void MarkLocationAsChecked(long locationID)
        {
            // Called when marking a location as checked remotely (i.e. through ReceiveItem, etc.)
            // This also grants items at said locations.
            AbstractPlacement pmt;
            ArchipelagoItemTag tag;
            if (!placementsByLocationID.TryGetValue(locationID, out pmt))
            {
                LogDebug($"Could not find a placement for location {locationID}");
                return;
            }

            foreach (AbstractItem item in pmt.Items)
            {
                if (!item.GetTag<ArchipelagoItemTag>(out tag)) continue;
                if (tag.Location != locationID) continue;
                if (item.WasEverObtained()) continue;

                // Soul items shouldn't be granted if the hero controller isn't instantiated, ItemChanger doesn't like that.
                if((item is SoulItem) && (HeroController.instance == null))
                {
                    // Just mark it as obtained instead
                    item.SetObtained();
                    return;
                }
                item.Give(pmt, RemoteGiveInfo);
            }
        }

        public void ReceiveItem(NetworkItem netItem)
        {
            AbstractPlacement pmt;
            var name = session.Items.GetItemName(netItem.Item);
            LogDebug($"Receiving item ID {netItem.Item}.  Name is {name}.  Slot is {netItem.Player}.  Location is {netItem.Location}.");

            if (netItem.Player == slot && netItem.Location > 0)
            {
                MarkLocationAsChecked(netItem.Location);
                return;
            }
            // If we're still here, this is an item from someone else.  We'll make up our own dummy placement and grant the item.
            AbstractItem item;
            item = Finder.GetItem(name);
            if (item == null)
            {
                LogDebug($"Could not find an item named '{name}'.  This means that item {netItem.Item} was not received.");
                return;
            }
            string sender;
            if (netItem.Location == -1)
            {
                sender = "Cheat Console";
            }
            else if (netItem.Location == -2)
            {
                sender = "Start";
            }
            else if (netItem.Player == 0)
            {
                sender = "Archipelago";
            }
            else
            {
                sender = session.Players.GetPlayerName(netItem.Player);
            }
            var itemName = item.UIDef.GetPostviewName();
            item.UIDef = ArchipelagoUIDef.CreateForReceivedItem(item, sender);
            item.name = $"{itemName} from {sender}";
            ArchipelagoLocation location = new(sender);
            pmt = location.Wrap();
            pmt.Add(item);
            InteropTag tag;
            tag = item.AddTag<InteropTag>();
            tag.Message = "RecentItems";
            tag.Properties["DisplayName"] = itemName;
            tag = pmt.AddTag<InteropTag>();
            tag.Message = "RecentItems";
            tag.Properties["DisplaySource"] = sender;
            ItemChangerMod.AddPlacements(pmt.Yield());
            if (netItem.Location == -2)
            {
                pmt.GiveAll(SilentGiveInfo);
            }
            else
            {
                pmt.GiveAll(RemoteGiveInfo);
            }
        }
        private void CreateItemPlacements()
        {
            void ScoutCallback(LocationInfoPacket packet)
            {
                MenuChanger.ThreadSupport.BeginInvoke(() =>
                {
                    DeferLocationChecks();
                    Dictionary<AbstractLocation, AbstractPlacement> placements = new();
                    foreach (var item in packet.Locations)
                    {
                        var locationName = session.Locations.GetLocationNameFromId(item.Location);
                        var itemName = session.Items.GetItemName(item.Item);

                        PlaceItem(locationName, itemName, item, placements);
                    }
                    ItemChangerMod.AddPlacements(placements.Values);
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

        public void PlaceItem(string location, string name, NetworkItem netItem, Dictionary<AbstractLocation, AbstractPlacement> placements)
        {
            LogDebug($"[PlaceItem] Placing item {name} into {location} with ID {netItem.Item}");
            
            var originalLocation = string.Copy(location);
            location = StripShopSuffix(location);
            
            AbstractLocation loc = Finder.GetLocation(location);
            if (loc == null)
            {
                LogDebug($"[PlaceItem] Location was null: Name: {location}.");
                return;
            }

            string recipientName = null;
            if (netItem.Player != slot)
            {
                recipientName = session.Players.GetPlayerName(netItem.Player);
            }

            AbstractPlacement pmt = placements.GetOrDefault(loc);
            if (pmt == null)
            {
                pmt = loc.Wrap();
                pmt.AddTag<ArchipelagoPlacementTag>();
                placements[loc] = pmt;
            }

            AbstractItem item;
            InteropTag tag;
            if (Finder.ItemNames.Contains(name))
            {
                // Items from Hollow Knight.
                item = Finder.GetItem(name);  // This is already a clone per what I can tell from IC source
                if (recipientName != null)
                {
                    tag = item.AddTag<InteropTag>();
                    tag.Message = "RecentItems";
                    tag.Properties["DisplayMessage"] = $"{item.UIDef.GetPostviewName()}\nsent to {recipientName}.";
                    item.UIDef = ArchipelagoUIDef.CreateForSentItem(item, recipientName);
                }
            else
            {
                // Items from other games.
                item = new ArchipelagoItem(name, recipientName, netItem.Flags);
                tag = item.AddTag<InteropTag>();
                tag.Message = "RecentItems";
                tag.Properties["DisplayMessage"] = $"{name}\nsent to {recipientName}.";
            }
            // Create a tag containing all AP-relevant information on the item.
            ArchipelagoItemTag itemTag;
            itemTag = item.AddTag<ArchipelagoItemTag>();
            itemTag.ReadNetItem(netItem);

            // Handle placement
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

            //ItemChangerMod.AddPlacements(pmt.Yield());
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

        private void ModHooks_SavegameLoadHook(int obj)
        {
            if (ApSettings == default)
            {
                return;
            }

            DeferLocationChecks();
            ConnectToArchipelago();
        }

        private void Events_OnItemChangerUnhook()
        {
            DisconnectArchipelago();
        }

        public void DisconnectArchipelago()
        {
            slot = 0;
            seed = 0;
            placementHandlers = null;

            if (session?.Socket != null && session.Socket.Connected)
            {
                session.Socket.DisconnectAsync();
            }

            session = null;
        }

        /// <summary>
        /// Begin deferring location checks.
        /// </summary>
        /// <remarks>
        /// During initial synchronization and other cases, we want to collect individual locations and send them as one batch.  This begins that process.
        /// </remarks>
        public void DeferLocationChecks()
        {
            deferringLocationChecks = true;
            LogDebug("Deferring location checks");
        }

        /// <summary>
        /// Stop deferring location checks.
        /// </summary>
        public void StopDeferringLocationChecks()
        {
            LogDebug("No longer deferring location checks");
            deferringLocationChecks = false;
            if (deferredLocationChecks.Any())
            {
                LogDebug($"Sending {deferredLocationChecks.Count} deferred location check(s).");
                session.Locations.CompleteLocationChecks(deferredLocationChecks.ToArray());
                deferredLocationChecks.Clear();
            }
        }

        /// <summary>
        /// Checks a single location or adds it to the deferred list.
        /// </summary>
        public void CheckLocation(long locationID)
        {
            if(locationID == 0)
            {
                throw new Exception("CheckLocation called with unspecified locationID.  This should never happen.");
            }
            if(deferringLocationChecks)
            {
                deferredLocationChecks.Add(locationID);
            }
            else
            {
                session.Locations.CompleteLocationChecks(locationID);
            }
        }

        /// <summary>
        /// Called when loading local (game-specific save data)
        /// </summary>
        /// <remarks>
        /// This is also called on the main menu screen with empty (defaulted) ConnectionDetails.  This will have an empty SlotName, so we treat this as a noop.
        /// </remarks>
        /// <param name="details"></param>
        public void OnLoadLocal(ConnectionDetails details)
        {
            if(details.SlotName == null || details.SlotName == "")  // Apparently, this is called even before a save is loaded.  Catch this.
            {
                return;
            }
            ApSettings = details;
        }

        /// <summary>
        /// Called when saving local (game-specific) save data.
        /// </summary>
        /// <returns></returns>
        public ConnectionDetails OnSaveLocal()
        {
            return ApSettings;
        }

        /// <summary>
        /// Called when loading global save data.
        /// </summary>
        /// <remarks>
        /// For simplicity's sake, we use the same data structure for both global and local save data, though not all fields are relevant in the global context.
        /// </remarks>
        /// <param name="details"></param>
        public void OnLoadGlobal(ConnectionDetails details)
        {
            ApSettings = details;
            ApSettings.ItemIndex = 0;
        }

        /// <summary>
        /// Called when saving global save data.
        /// </summary>
        /// <returns></returns>
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