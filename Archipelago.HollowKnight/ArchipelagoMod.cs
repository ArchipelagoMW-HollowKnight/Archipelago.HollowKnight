using Archipelago.HollowKnight.IC.Modules;
using Archipelago.HollowKnight.MC;
using Archipelago.HollowKnight.SlotDataModel;
using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Enums;
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
    public class ArchipelagoMod : Mod, IGlobalSettings<APGlobalSettings>, ILocalSettings<APLocalSettings>
    {
        // Events support
        public static event Action OnArchipelagoGameStarted;
        public static event Action OnArchipelagoGameEnded;

        /// <summary>
        /// Minimum Archipelago Protocol Version
        /// </summary>
        private readonly Version ArchipelagoProtocolVersion = new(0, 5, 0);

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
        public static ArchipelagoMod Instance;
        public ArchipelagoSession session { get; private set; }
        public SlotData SlotData { get; private set; }
        public bool ArchipelagoEnabled { get; set; }

        internal SpriteManager spriteManager;

        internal APGlobalSettings GS = new();
        internal APLocalSettings LS = new();

        public ArchipelagoMod() : base("Archipelago") { }

        public override void Initialize(Dictionary<string, Dictionary<string, GameObject>> preloadedObjects)
        {
            base.Initialize();
            Log("Initializing");
            Instance = this;
            spriteManager = new SpriteManager(typeof(ArchipelagoMod).Assembly, "Archipelago.HollowKnight.Resources.");

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
            LS = new();

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

            if (randomize)
            {
                LogDebug("StartOrResumeGame: Beginning first time randomization.");
                LS.ItemIndex = 0;
                LS.Seed = SlotData.Seed;
                LS.RoomSeed = session.RoomState.Seed;

                LogDebug($"StartOrResumeGame: Room: {LS.RoomSeed}; Seed = {LS.RoomSeed}");

                ArchipelagoRandomizer randomizer = new(SlotData);
                randomizer.Randomize();
            }
            else
            {
                LogDebug($"StartOrResumeGame: Local : Room: {LS.RoomSeed}; Seed = {LS.Seed}");
                int seed = SlotData.Seed;
                LogDebug($"StartOrResumeGame: AP    : Room: {session.RoomState.Seed}; Seed = {seed}");
                if (seed != LS.Seed || session.RoomState.Seed != LS.RoomSeed)
                {
                    throw new LoginValidationException("Slot mismatch. Saved seed does not match the server value. Is this the correct save?");
                }
            }

            // check the goal is one we know how to cope with
            if (SlotData.Options.Goal > GoalsLookup.MAX)
            {
                throw new LoginValidationException($"Unrecognized goal condition {SlotData.Options.Goal} (are you running an outdated client?)");
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
            session = ArchipelagoSessionFactory.CreateSession(LS.ConnectionDetails.ServerUrl, LS.ConnectionDetails.ServerPort);

            LoginResult loginResult = session.TryConnectAndLogin("Hollow Knight",
                                                         LS.ConnectionDetails.SlotName,
                                                         ItemsHandlingFlags.AllItems,
                                                         ArchipelagoProtocolVersion,
                                                         password: LS.ConnectionDetails.ServerPassword,
                                                         requestSlotData: false);

            if (loginResult is LoginFailure failure)
            {
                string errors = string.Join(", ", failure.Errors);
                LogError($"Unable to connect to Archipelago because: {string.Join(", ", failure.Errors)}");
                throw new LoginValidationException(errors);
            }
            else if (loginResult is LoginSuccessful success)
            {
                SlotData = session.DataStorage.GetSlotData<SlotData>();
                session.Socket.SocketClosed += OnSocketClosed;

                return success;
            }
            else
            {
                LogError($"Unexpected LoginResult type when connecting to Archipelago: {loginResult}");
                throw new LoginValidationException("Unexpected login result.");
            }
        }

        public void DisconnectArchipelago()
        {
            if (session?.Socket != null)
            {
                session.Socket.SocketClosed -= OnSocketClosed;
            }

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
        public void OnLoadLocal(APLocalSettings ls)
        {
            if (ls.ConnectionDetails == null
                || ls.ConnectionDetails.SlotName == null
                || ls.ConnectionDetails.SlotName == "") // Apparently, this is called even before a save is loaded.  Catch this.
            {
                return;
            }

            LS = ls;
        }

        /// <summary>
        /// Called when saving local (game-specific) save data.
        /// </summary>
        /// <returns></returns>
        public APLocalSettings OnSaveLocal()
        {
            if (!ArchipelagoEnabled)
            {
                return default;
            }

            return LS;
        }

        /// <summary>
        /// Called when loading global save data.
        /// </summary>
        /// <remarks>
        /// For simplicity's sake, we use the same data structure for both global and local save data, though not all fields are relevant in the global context.
        /// </remarks>
        /// <param name="details"></param>
        public void OnLoadGlobal(APGlobalSettings gs)
        {
            GS = gs;
        }

        /// <summary>
        /// Called when saving global save data.
        /// </summary>
        /// <returns></returns>
        public APGlobalSettings OnSaveGlobal()
        {
            return GS with
            {
                MenuConnectionDetails = GS.MenuConnectionDetails with { ServerPassword = null }
            };
        }
    }
}
