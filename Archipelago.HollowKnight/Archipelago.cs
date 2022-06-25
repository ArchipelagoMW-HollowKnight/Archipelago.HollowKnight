using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Archipelago.HollowKnight.IC;
using Archipelago.HollowKnight.MC;
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
using Modding;
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
        public override string GetVersion() => new Version(0, 0, 3, 1).ToString();
        public static Archipelago Instance;
        public SlotOptions SlotOptions { get; set; }
        public bool ArchipelagoEnabled { get; set; }

        public int Slot => slot;
        public string Player => session.Players.GetPlayerName(slot);

        public static Sprite Sprite;
        public static Sprite SmallSprite;
        public static Sprite DeathLinkSprite;
        internal static FieldInfo obtainStateFieldInfo;

        internal SpriteManager spriteManager;
        internal ConnectionDetails MenuSettings = new()
        {
            ServerUrl = "archipelago.gg",
            ServerPort = 38281,
        };
        internal ConnectionDetails ApSettings = new();

        internal ArchipelagoSession session;

        /// <summary>
        /// Allows lookup of a placement by its location ID number.  Used during syncing and shared-slot coop.
        /// </summary>
        internal readonly Dictionary<long, AbstractPlacement> placementsByLocationID = new();

        /// <summary>
        /// List of pending locations.
        /// </summary>
        private readonly HashSet<long> deferredLocationChecks = new();

        public bool DeferringLocationChecks { get => deferringLocationChecks; }
        private bool deferringLocationChecks = false;

        private int pendingGeo = 0;

        private int slot;
        private TimeSpan timeBetweenReceiveItem = TimeSpan.FromMilliseconds(500);
        private DateTime lastUpdate = DateTime.MinValue;
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

        // Events support
        public static event Action OnArchipelagoGameStarted;
        public static event Action OnArchipelagoGameEnded;

        // Shade position fixes
        public static readonly Dictionary<string, (float x, float y)> ShadeSpawnPositionFixes = new()
        {
            { "Abyss_08", (90.0f, 90.0f) },  // Lifeblood Core room.  Even outside of deathlink, shades spawn out of bounds.
            { "Room_Colosseum_Spectate", (124.0f, 10.0f) },  // Shade spawns inside inaccessible arena
            { "Resting_Grounds_09", (7.4f, 10.0f) },  // Shade spawns underground.
            { "Runes1_18", (11.5f, 23.0f) },  // Shade potentially spawns on the wrong side of an inaccessible gate.
        };

        public override void Initialize(Dictionary<string, Dictionary<string, GameObject>> preloadedObjects)
        {
            base.Initialize();
            Log("Initializing");
            Instance = this;
            spriteManager = new SpriteManager(typeof(Archipelago).Assembly, "Archipelago.HollowKnight.Resources.");
            Sprite = spriteManager.GetSprite("Icon");
            SmallSprite = spriteManager.GetSprite("IconSmall");
            DeathLinkSprite = spriteManager.GetSprite("DeathLinkIcon");
            obtainStateFieldInfo = typeof(AbstractItem).GetField("obtainState", BindingFlags.NonPublic | BindingFlags.Instance);

            MenuChanger.ModeMenu.AddMode(new ArchipelagoModeMenuConstructor());

            ModHooks.SavegameLoadHook += ModHooks_SavegameLoadHook;
            Log("Initialized");
        }

        private void HeroController_Start(On.HeroController.orig_Start orig, HeroController self)
        {
            orig(self);
            SynchronizeCheckedLocations();
            StopDeferringLocationChecks();
            if(pendingGeo > 0)
            {
                self.AddGeo(pendingGeo);
                pendingGeo = 0;
            }
        }

        private void SynchronizeCheckedLocations()
        {
            if (ArchipelagoEnabled)
            {
                DeferLocationChecks();
                while (ReceiveNextItem());  // Receive items until the queue is empty.

                foreach(long location in session.Locations.AllLocationsChecked)
                {
                    MarkLocationAsChecked(location);
                }
            }
        }

        public void DeclareVictory()
        {
            LogDebug($"Declaring victory if ArchipelagEnabled.  ArchipelagoEnabled = {ArchipelagoEnabled}");
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
            if (deferringLocationChecks)
            {
                StopDeferringLocationChecks();
            }

            if (DateTime.Now - timeBetweenReceiveItem > lastUpdate && session.Items.Any())
            {
                ReceiveNextItem();
            }
        }

        public void EndGame()
        {
            LogDebug("Ending Archipelago game");
            try
            {
                OnArchipelagoGameEnded?.Invoke();
            }
            catch (Exception ex)
            {
                LogError($"Error invoking OnArchipelagoGameEnded:\n {ex}");
            }

            DisconnectArchipelago();
            ArchipelagoEnabled = false;
            ApSettings = new();

            ItemChanger.Events.OnItemChangerUnhook -= EndGame;
            ModHooks.HeroUpdateHook -= ModHooks_HeroUpdateHook;
            ModHooks.AfterPlayerDeadHook -= ModHooks_AfterPlayerDeadHook;
            On.HeroController.Start -= HeroController_Start;

            if (goal != null)
            {
                goal.Unselect();
                goal = null;
            }
        }

        /// <summary>
        /// Call when starting or resuming a game to randomize and restore state.
        /// </summary>
        public void StartOrResumeGame(bool randomize)
        {
            if (!ArchipelagoEnabled)
            {
                LogDebug("StartOrResumeGame: This is not an Archipelago Game, so not doing anything.");
                return;
            }
            LogDebug("StartOrResumeGame: This is an Archipelago Game.");

            ItemChanger.Events.OnItemChangerUnhook += EndGame;
            ModHooks.HeroUpdateHook += ModHooks_HeroUpdateHook;
            ModHooks.AfterPlayerDeadHook += ModHooks_AfterPlayerDeadHook;
            On.HeroController.Start += HeroController_Start;

            LoginSuccessful loginResult = ConnectToArchipelago() as LoginSuccessful;
            DeferLocationChecks();
            if (randomize)
            {
                LogDebug("StartOrResumeGame: Beginning first time randomization.");
                ApSettings.ItemIndex = 0;
                ApSettings.Seed = (long)loginResult.SlotData["seed"];
                ApSettings.RoomSeed = session.RoomState.Seed;

                LogDebug($"StartOrResumeGame: Room: {ApSettings.RoomSeed}; Seed = {ApSettings.RoomSeed}");

                var randomizer = new ArchipelagoRandomizer(loginResult.SlotData);
                randomizer.Randomize();
                pendingGeo = SlotOptions.StartingGeo;
            }
            else
            {
                LogDebug($"StartOrResumeGame: Local : Room: {ApSettings.RoomSeed}; Seed = {ApSettings.Seed}");
                var seed = (long)loginResult.SlotData["seed"];
                LogDebug($"StartOrResumeGame: AP    : Room: {session.RoomState.Seed}; Seed = {seed}");
                if (seed != ApSettings.Seed || session.RoomState.Seed != ApSettings.RoomSeed)
                {
                    UIManager.instance.UIReturnToMainMenu();
                    throw new ArchipelagoConnectionException("Slot mismatch.  Saved seed does not match the server value.  Is this the correct save?");
                }
                pendingGeo = 0;
            }
            // Discard from the beginning of the incoming item queue up to how many items we have received.
            for (int i = 0; i < ApSettings.ItemIndex; ++i)
            {
                NetworkItem netItem = session.Items.DequeueItem();
                LogDebug($"Fast-forwarding past an already-acquired {session.Items.GetItemName(netItem.Item)}");
            }
            try
            {
                goal = Goal.GetGoal(SlotOptions.Goal);
            }
            catch (ArgumentOutOfRangeException ex)
            {
                UIManager.instance.UIReturnToMainMenu();
                LogError($"Listed goal is {SlotOptions.Goal}, which is greater than {GoalsLookup.MAX}.  Is this an outdated client?");
                throw ex;
            }
            goal.Select();

            try
            {
                OnArchipelagoGameStarted?.Invoke();
            } catch (Exception ex)
            {
                LogError($"Error invoking OnArchipelagoGameStarted:\n {ex}");
            }
        }

        private LoginResult ConnectToArchipelago()
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
                slot = success.Slot;
                SlotOptions = SlotDataExtract.ExtractObjectFromSlotData<SlotOptions>(success.SlotData["options"]);
                LogDebug($"Deathlink type: {SlotOptions.DeathLink}");

                // Enable Deathlink
                if(SlotOptions.DeathLink != DeathLinkType.None)
                {
                    DeathLinkSupport.Instance.Enable();
                }
                else
                {
                    DeathLinkSupport.Instance.Disable();
                }
                return loginResult;
            } 
            else
            {
                LogError($"Unexpected LoginResult type when connecting to Archipelago: {loginResult}");
                throw new ArchipelagoConnectionException("Unexpected login result.");
            }
        }

        private System.Collections.IEnumerator HeroController_Respawn(On.HeroController.orig_Respawn orig, HeroController self)
        {
            On.HeroController.Respawn -= HeroController_Respawn;
            return orig(self);
        }

        public void MarkLocationAsChecked(long locationID)
        {
            // Called when marking a location as checked remotely (i.e. through ReceiveItem, etc.)
            // This also grants items at said locations.
            AbstractPlacement pmt;
            ArchipelagoItemTag tag;
            bool hadNewlyObtainedItems = false;
            bool hadUnobtainedItems = false;

            LogDebug($"Marking location {locationID} as checked.");
            if (!placementsByLocationID.TryGetValue(locationID, out pmt))
            {
                LogDebug($"Could not find a placement for location {locationID}");
                return;
            }

            foreach (AbstractItem item in pmt.Items)
            {
                if (!item.GetTag<ArchipelagoItemTag>(out tag))
                {
                    hadUnobtainedItems = true;
                    continue;
                }
                if (item.WasEverObtained())
                {
                    continue;
                }
                if (tag.Location != locationID)
                {
                    hadUnobtainedItems = true;
                    continue;
                }

                hadNewlyObtainedItems = true;
                pmt.AddVisitFlag(VisitState.ObtainedAnyItem);

                // Soul items shouldn't be granted if the hero controller isn't instantiated, ItemChanger doesn't like that.
                if ((item is SoulItem) && (HeroController.instance == null))
                {
                    // Just mark it as obtained instead
                    item.SetObtained();
                }
                else
                {
                    item.Give(pmt, deferringLocationChecks ? SilentGiveInfo : RemoteGiveInfo);
                }
            }

            if(hadNewlyObtainedItems && !hadUnobtainedItems)
            {
                pmt.AddVisitFlag(VisitState.Opened | VisitState.Dropped | VisitState.Accepted | VisitState.ObtainedAnyItem);
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

        private void ModHooks_SavegameLoadHook(int obj)
        {
            ArchipelagoEnabled = ApSettings.ServerUrl != "" && ApSettings.ServerPort != 0 && ApSettings.SlotName != "";
            StartOrResumeGame(false);  // No-op if AP disabled.
        }

        /// <summary>
        /// With DeathLink (and possibly with future trap implementations), dying in certain locations can produce inaccessible shades.  Fix that.
        /// </summary>

        private void ModHooks_AfterPlayerDeadHook()
        {
            // Fixes up some bad shade placements by vanilla HK.
            PlayerData pd = PlayerData.instance;
            if (ShadeSpawnPositionFixes.TryGetValue(pd.shadeScene, out (float x, float y) position))
            {
                pd.shadePositionX = position.x;
                pd.shadePositionY = position.y;
            }
        }

        public void DisconnectArchipelago()
        {
            DeathLinkSupport.Instance.Disable();
            slot = 0;

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
        /// 
        /// Locations marked as obtained during deferred location checks will also have their messaging suppressed unless they are for our own world.
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
                session.Locations.CompleteLocationChecksAsync(null, locationID);
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
            if(!ArchipelagoEnabled)
            {
                return default;
            }
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
            MenuSettings = details;
            MenuSettings.ItemIndex = 0;
        }

        /// <summary>
        /// Called when saving global save data.
        /// </summary>
        /// <returns></returns>
        public ConnectionDetails OnSaveGlobal()
        {
            return new ConnectionDetails()
            {
                ServerUrl = MenuSettings.ServerUrl,
                ServerPort = MenuSettings.ServerPort,
                SlotName = MenuSettings.SlotName
            };
        }
    }
}