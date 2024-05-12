using Archipelago.HollowKnight.IC.Modules;
using Archipelago.HollowKnight.MC;
using Archipelago.HollowKnight.SlotData;
using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.Models;
using Archipelago.MultiClient.Net.Packets;
using ItemChanger;
using ItemChanger.Internal;
using Modding;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Archipelago.HollowKnight
{
    public class Archipelago : Mod, IGlobalSettings<ConnectionDetails>, ILocalSettings<ConnectionDetails>, IMenuMod
    {
        // Events support
        public static event Action OnArchipelagoGameStarted;
        public static event Action OnArchipelagoGameEnded;

        /// <summary>
        /// Minimum Archipelago Protocol Version
        /// </summary>
        private readonly Version ArchipelagoProtocolVersion = new(0, 4, 4);

        /// <summary>
        /// Mod version as reported to the modding API
        /// </summary>
        public override string GetVersion()
        {
            Version assemblyVersion = GetType().Assembly.GetName().Version;
            string version = $"{assemblyVersion.Major}.{assemblyVersion.Minor}.{assemblyVersion.Build}";
#if DEBUG
            using SHA1 sha = SHA1.Create();
            using FileStream str = File.OpenRead(GetType().Assembly.Location);
            StringBuilder sb = new();
            foreach (byte b in sha.ComputeHash(str).Take(4))
            {
                sb.AppendFormat("{0:x2}", b);
            }
            version += "-prerelease+" + sb.ToString();
#endif
            return version;
        }
        public static Archipelago Instance;
        public ArchipelagoSession session { get; private set; }
        public SlotOptions SlotOptions { get; set; }
        public int GrubHuntRequiredGrubs { get; set; }
        public bool ArchipelagoEnabled { get; set; }

        public int Slot { get; private set; }
        public IReadOnlyDictionary<int, NetworkSlot> AllSlots { get; private set; }
        public string Player => session.Players.GetPlayerName(Slot);

        internal SpriteManager spriteManager;

        internal ConnectionDetails MenuSettings = new()
        {
            ServerUrl = "archipelago.gg",
            ServerPort = 38281,
        };

        internal ConnectionDetails ApSettings = new();

        public override void Initialize(Dictionary<string, Dictionary<string, GameObject>> preloadedObjects)
        {
            base.Initialize();
            Log("Initializing");
            Instance = this;
            spriteManager = new SpriteManager(typeof(Archipelago).Assembly, "Archipelago.HollowKnight.Resources.");

            MenuChanger.ModeMenu.AddMode(new ArchipelagoModeMenuConstructor());
            Log("Initialized");
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

            Events.OnItemChangerUnhook -= EndGame;
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

            LoginSuccessful loginResult = ConnectToArchipelago();
            if (loginResult.SlotData.TryGetValue("grub_count", out object objGrubsRequired))
            {
                GrubHuntRequiredGrubs = (int)((long) objGrubsRequired);
            }
            else
            {
                // if not present, it's an older world version so make the goal functionally impossible
                // (e.g. for Any goal)
                GrubHuntRequiredGrubs = int.MaxValue;
            }

            if (randomize)
            {
                LogDebug("StartOrResumeGame: Beginning first time randomization.");
                ApSettings.ItemIndex = 0;
                ApSettings.Seed = (long) loginResult.SlotData["seed"];
                ApSettings.RoomSeed = session.RoomState.Seed;

                LogDebug($"StartOrResumeGame: Room: {ApSettings.RoomSeed}; Seed = {ApSettings.RoomSeed}");

                ArchipelagoRandomizer randomizer = new(loginResult.SlotData);
                randomizer.Randomize();
            }
            else
            {
                LogDebug($"StartOrResumeGame: Local : Room: {ApSettings.RoomSeed}; Seed = {ApSettings.Seed}");
                long seed = (long) loginResult.SlotData["seed"];
                LogDebug($"StartOrResumeGame: AP    : Room: {session.RoomState.Seed}; Seed = {seed}");
                if (seed != ApSettings.Seed || session.RoomState.Seed != ApSettings.RoomSeed)
                {
                    if (ApSettings.RoomSeed == null)
                    {
                        LogWarn(
                            "Are you upgrading from a previous version? Seed data did not exist in save. It does now.");
                        ApSettings.Seed = seed;
                        ApSettings.RoomSeed = session.RoomState.Seed;
                    }
                    else
                    {
                        throw new LoginValidationException("Slot mismatch. Saved seed does not match the server value. Is this the correct save?");
                    }
                }
            }

            // check the goal is one we know how to cope with
            if (SlotOptions.Goal > GoalsLookup.MAX)
            {
                throw new LoginValidationException($"Unrecognized goal condition {SlotOptions.Goal} (are you running an outdated client?)");
            }

            // Hooks happen after we've definitively connected to an Archipelago slot correctly.
            // Doing this before checking for the correct slot/seed/room will cause problems if
            // the client connects to the wrong session with a matching slot.
            Events.OnItemChangerUnhook += EndGame;

            try
            {
                OnArchipelagoGameStarted?.Invoke();
            }
            catch (Exception ex)
            {
                LogError($"Error invoking OnArchipelagoGameStarted:\n {ex}");
            }
        }

        private void OnSocketClosed(string reason)
        {
            ItemChangerMod.Modules.Get<ItemNetworkingModule>().ReportDisconnect();
        }

        private LoginSuccessful ConnectToArchipelago()
        {
            session = ArchipelagoSessionFactory.CreateSession(ApSettings.ServerUrl, ApSettings.ServerPort);
            session.Socket.PacketReceived += OnPacketReceived;

            LoginResult loginResult = session.TryConnectAndLogin("Hollow Knight",
                                                         ApSettings.SlotName,
                                                         ItemsHandlingFlags.AllItems,
                                                         ArchipelagoProtocolVersion,
                                                         password: ApSettings.ServerPassword);

            if (loginResult is LoginFailure failure)
            {
                string errors = string.Join(", ", failure.Errors);
                LogError($"Unable to connect to Archipelago because: {string.Join(", ", failure.Errors)}");
                throw new LoginValidationException(errors);
            }
            else if (loginResult is LoginSuccessful success)
            {
                // Read slot data.
                Slot = success.Slot;
                SlotOptions = SlotDataExtract.ExtractObjectFromSlotData<SlotOptions>(success.SlotData["options"]);
                session.Socket.SocketClosed += OnSocketClosed;

                return success;
            }
            else
            {
                LogError($"Unexpected LoginResult type when connecting to Archipelago: {loginResult}");
                throw new LoginValidationException("Unexpected login result.");
            }
        }

        private void OnPacketReceived(ArchipelagoPacketBase packet)
        {
            if (packet is ConnectedPacket cp)
            {
                session.Socket.PacketReceived -= OnPacketReceived;
                AllSlots = cp.SlotInfo;
            }
        }

        public void DisconnectArchipelago()
        {
            if (session?.Socket != null)
            {
                session.Socket.SocketClosed -= OnSocketClosed;
            }

            Slot = 0;
            AllSlots = null;

            if (session?.Socket != null && session.Socket.Connected)
            {
                session.Socket.DisconnectAsync();
            }

            session = null;
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
            if (details.SlotName == null ||
                details.SlotName == "") // Apparently, this is called even before a save is loaded.  Catch this.
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
            if (!ArchipelagoEnabled)
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
                SlotName = MenuSettings.SlotName,
                AlwaysShowItems = MenuSettings.AlwaysShowItems,
            };
        }

        public bool ToggleButtonInsideMenu => false;
        public List<IMenuMod.MenuEntry> GetMenuData(IMenuMod.MenuEntry? toggleButtonEntry)
        {
            return [
                new IMenuMod.MenuEntry {
                    Name = "Show offline items",
                    Description = "Show items received while you were disconnected.",
                    Values = ["No", "Yes"],
                    Saver = opt => { MenuSettings.AlwaysShowItems = opt == 1; },
                    Loader = () => MenuSettings.AlwaysShowItems ? 1 : 0,
                },
            ];
        }
    }
}
