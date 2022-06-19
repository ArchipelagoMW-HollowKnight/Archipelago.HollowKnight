using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Archipelago.HollowKnight.IC;
using Archipelago.HollowKnight.MC;
using Archipelago.HollowKnight.Placements;
using Archipelago.HollowKnight.SlotData;
using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.BounceFeatures.DeathLink;
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

        private ShadeInfo shade = null;
        private DeathLinkStatus deathlinkStatus = DeathLinkStatus.None;
        private DeathLinkService deathLinkService = null;
        private System.Random deathLinkRandom = new();  // This is only messaging, so does not need to be seeded.
        private int outgoingDeathlinks = 0;
        private int lastDamageType = 0;
        private DateTime lastDamageTime = DateTime.MinValue;

        public int Slot { get => slot; }

        internal static Sprite Sprite;
        internal static Sprite SmallSprite;
        internal static Sprite DeathLinkSprite;
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

            if (deathlinkStatus == DeathLinkStatus.Pending && CanMurderPlayer())
            {
                MurderPlayer();
            }
        }

        public bool CanMurderPlayer()
        {
            HeroController hc = HeroController.instance;
            return hc.acceptingInput && hc.damageMode == GlobalEnums.DamageMode.FULL_DAMAGE;
        }

        public void MurderPlayer()
        {
            HeroController hc = HeroController.instance;
            string scene = GameManager.instance.sceneName;
            LogDebug($"So, somebody else has chosen... death.  Current scene: {scene}");

            bool switcharoo = (!(
                // These are all conditions where the death should be processed like a vanilla death with no shade switcharoo
                SlotOptions.DeathLink == DeathLinkType.Vanilla
                || (SlotOptions.DeathLink == DeathLinkType.Shade && PlayerData.instance.shadeScene == "None")
                || (scene.StartsWith("Dream_") && scene != SceneNames.Dream_Final_Boss)  // Most dream sequences, except Radiance
                || scene.StartsWith("GG_")  // All Godhome sequences
                || scene.StartsWith("White_Palace_")  // White Palace is a dream.
                || scene == SceneNames.Grimm_Nightmare  // NKG
            ));

            if (switcharoo && scene == "Room_Colosseum_Spectate")
            {
                switcharoo = false;
            }
            if (switcharoo)
            {
                // Do the shade switcharoo
                deathlinkStatus = DeathLinkStatus.Dying;
                shade = ShadeInfo.FromPlayerData();
            }
            else
            {
                deathlinkStatus = DeathLinkStatus.None;
            }
            HeroController.instance.TakeDamage(HeroController.instance.gameObject, GlobalEnums.CollisionSide.other, 9999, 0);
        }

        public void SendDeathLink()
        {
            if (deathLinkService == null)
            {
                return;
            }

            if ((DateTime.UtcNow - lastDamageTime).TotalSeconds > 5000)
            {
                // Damage source was more than 5 seconds ago, so ignore damage type
                lastDamageType = 0;
            }

            var player = session.Players.GetPlayerName(slot);

            // Messages that are valid options all the time.
            List<string> deathMessages = new()
            {
                "@ died.",
                "@ has perished.",
                "@ made poor life choices.",
                "@ didn't listen to Hornet's advice.",
                "@ took damage equal to or more than their current HP.",
                "@ made a fatal mistake.",
                "@ threw some shade at @.",
            };

            // Additional messages
            switch (lastDamageType)
            {
                case 1:  // Enemy damage
                    deathMessages.AddRange(new[] {
                        "@ has discovered that there are bugs in Hallownest.",
                        "@ should have dodged.",
                        "@ should have jumped.",
                        "@ significantly mistimed their parry attempt.",
                        "@ should have considered equipping Dreamshield.",
                        "@ must have never fought that enemy before.",
                        "@ did not make it to phase 2.",
                    });
                break;
                case 2:  // Spikes
                    deathMessages.AddRange(new[] {
                        "@ was in the wrong place.",
                        "@ mistimed their jump.",
                        "@ didn't see the sharp things.",
                        "@ didn't see that saw.",
                        "@ fought the spikes and the spikes won.",
                    });
                break;
                case 3:  // Acid, probably also water.
                    deathMessages.AddRange(new[] {
                        "@ was in the wrong place.",
                        "@ mistimed their jump.",
                        "@ forgot their floaties.",
                        "What @ thought was H2O was H2SO4.",
                        "@ wishes they could swim.",
                        "@ used the wrong kind of dive.",
                        "@ got into a fight with a pool of liquid and lost.",
                    });
                break;
                default:  // Unspecified death message types.
                    deathMessages.AddRange(new[] {
                        "@ has died in a manner most unusual.",
                        "@ found a way to break the game, and the game broke @ back.",
                        "@ has lost The Game",
                    });
                    break;
            }

            // Choose a deathlink message
            outgoingDeathlinks += 1;
            var message = deathMessages[deathLinkRandom.Next(0, deathMessages.Count)].Replace("@", player);
            deathLinkService.SendDeathLink(new(player, message));
        }

        public void EndGame()
        {
            LogDebug("Ending Archipelago game");
            DisconnectArchipelago();
            ArchipelagoEnabled = false;
            ApSettings = new();

            ItemChanger.Events.OnItemChangerUnhook -= EndGame;
            ModHooks.HeroUpdateHook -= ModHooks_HeroUpdateHook;
            ModHooks.BeforePlayerDeadHook -= ModHooks_BeforePlayerDeadHook;
            ModHooks.AfterPlayerDeadHook -= ModHooks_AfterPlayerDeadHook;
            On.HeroController.Start -= HeroController_Start;
            On.HeroController.TakeDamage -= HeroController_TakeDamage;

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
            ModHooks.BeforePlayerDeadHook += ModHooks_BeforePlayerDeadHook;
            ModHooks.AfterPlayerDeadHook += ModHooks_AfterPlayerDeadHook;
            On.HeroController.Start += HeroController_Start;
            On.HeroController.TakeDamage += HeroController_TakeDamage;

            LoginSuccessful loginResult = ConnectToArchipelago() as LoginSuccessful;
            DeferLocationChecks();
            if (randomize)
            {
                LogDebug("StartOrResumeGame: Beginning first time randomization.");
                ApSettings.ItemIndex = 0;

                var randomizer = new ArchipelagoRandomizer(loginResult.SlotData);
                randomizer.Randomize();
                pendingGeo = SlotOptions.StartingGeo;
            }
            else
            {
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
                LogError($"Listed goal is {SlotOptions.Goal}, which is greater than {GoalsLookup.MAX}.  Is this an outdated client?");
                throw ex;
            }
            goal.Select();
        }

        private void HeroController_TakeDamage(On.HeroController.orig_TakeDamage orig, HeroController self, GameObject go, GlobalEnums.CollisionSide damageSide, int damageAmount, int hazardType)
        {
            orig(self, go, damageSide, damageAmount, hazardType);
            lastDamageTime = DateTime.UtcNow;
            lastDamageType = hazardType;
        }

        private void ModHooks_BeforePlayerDeadHook()
        {
            // If it's a deathlink death, do nothing.
            if (deathlinkStatus == DeathLinkStatus.Dying)
            {
                return;
            }
            SendDeathLink();
        }


        private void ModHooks_AfterPlayerDeadHook()
        {
            LogDebug("Oh.  You died.");
            if (deathlinkStatus != DeathLinkStatus.Dying)
            {
                deathlinkStatus = DeathLinkStatus.None;
                shade = ShadeInfo.FromPlayerData();
                if (shade != null)
                {
                    LogDebug($"Detected normal death at scene {shade.Scene}.  Lost {shade.Geo} geo.");
                }
                return;
            }
            deathlinkStatus = DeathLinkStatus.None;
            PlayerData pd = PlayerData.instance;
            LogDebug($"Dying due to deathlink.  Recovering {pd.geoPool} lost geo.");
            HeroController.instance.AddGeoQuietly(pd.geoPool);
            pd.geoPool = 0;
            if(shade == null || shade.Scene == "None") 
            {
                LogDebug("No saved shade info, so deleting the new one.");
                GameManager.instance.EndSoulLimiter();
                PlayerData.instance.soulLimited = false;
            }
            else
            {
                LogDebug("Had saved shade data, so restoring it.");
                LogDebug($"Respawning shade at {shade.Scene} with {shade.Geo} geo.");
                shade.WritePlayerData();
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
                    deathLinkService = session.CreateDeathLinkServiceAndEnable();
                    deathLinkService.OnDeathLinkReceived += OnDeathLinkReceived;
                    session.UpdateConnectionOptions(new[] { "DeathLink" }, ItemsHandlingFlags.AllItems);
                }
                else
                {
                    deathLinkService = null;
                }
                return loginResult;
            } 
            else
            {
                LogError($"Unexpected LoginResult type when connecting to Archipelago: {loginResult}");
                throw new ArchipelagoConnectionException("Unexpected login result.");
            }
        }

        private void OnDeathLinkReceived(DeathLink deathLink)
        {
            if (outgoingDeathlinks > 0)
            {
                outgoingDeathlinks--;
                return;
            }
            if (deathlinkStatus == DeathLinkStatus.None)
            {
                deathlinkStatus = DeathLinkStatus.Pending;
            }
            string cause = deathLink.Cause;
            if (cause == null || cause == "")
            {
                cause = $"{deathLink.Source} died.";
            }
            new MsgUIDef()
            {
                name = new BoxedString(cause),
                sprite = new BoxedSprite(DeathLinkSprite)
//                    GameCameras.instance.hudCamera.GetComponentsInChildren<JournalEntryStats>(true)
//                    .Where(x => x.notesConvo == "NOTE_HOLLOW_SHADE")
//                    .First()
//                    .GetSprite()
//                )
            }.SendMessage(MessageType.Corner, null);
/*
            ArchipelagoLocation location = new(deathLink.Source);
            AbstractPlacement pmt = location.Wrap();
            pmt.Add(new DeathlinkItem(deathLink.Source, deathLink.Cause));
            var tag = pmt.AddTag<InteropTag>();
            tag.Properties["DisplaySource"] = deathLink.Source;
            ItemChangerMod.AddPlacements(pmt.Yield());
            pmt.GiveAll(RemoteGiveInfo);
*/
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

        public void DisconnectArchipelago()
        {
            if(deathLinkService != null)
            {
                deathLinkService.OnDeathLinkReceived -= OnDeathLinkReceived;
                deathLinkService = null;
            }
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