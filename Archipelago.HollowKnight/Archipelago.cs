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
using ItemChanger.Tags;
using ItemChanger.UIDefs;
using Modding;
using Newtonsoft.Json;
using UnityEngine;

namespace Archipelago.HollowKnight
{
    public partial class Archipelago : Mod, ILocalSettings<ConnectionDetails>
    {
        private readonly Version ArchipelagoProtocolVersion = new Version(0, 2, 6);

        internal static Archipelago Instance;
        internal SpriteManager spriteManager;
        internal static Sprite Sprite;
        internal ConnectionDetails ApSettings;
        internal bool ArchipelagoEnabled = false;

        internal ArchipelagoSession session;
        private Dictionary<string, AbstractPlacement> vanillaItemPlacements = new();

        public override string GetVersion() => new Version(0, 0, 1).ToString();

        public override void Initialize(Dictionary<string, Dictionary<string, GameObject>> preloadedObjects)
        {
            base.Initialize();
            Log("Initializing");

            Instance = this;
            spriteManager = new SpriteManager(typeof(Archipelago).Assembly, "Archipelago.HollowKnight.Resources.");
            Sprite = spriteManager.GetSprite("IconMono");

            MenuChanger.ModeMenu.AddMode(new ArchipelagoModeMenuConstructor());

            ModHooks.SavegameLoadHook += ModHooks_SavegameLoadHook;
            On.UIManager.StartNewGame += UIManager_StartNewGame;
            Events.OnItemChangerUnhook += Events_OnItemChangerUnhook;

            Log("Initialized");
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
        }

        private void Items_ItemReceived(ReceivedItemsHelper helper)
        {
            var itemReceived = helper.DequeueItem();
            ReceiveItem(itemReceived.Item);
        }

        public void ReceiveItem(int id)
        {
            var name = session.Items.GetItemName(id);
            if (vanillaItemPlacements.TryGetValue(name, out var placement))
            {
                placement.GiveAll(new GiveInfo()
                {
                    FlingType = FlingType.DirectDeposit,
                    Container = Container.Unknown,
                    MessageType = MessageType.Corner
                });
            }
        }

        private void CreateItemPlacements()
        {
            //TODO: Debug this, items not being placed properly. Maybe item names are wrong.
            void ScoutCallback(LocationInfoPacket packet)
            {
                foreach (var item in packet.Locations)
                {
                    var locationName = session.Locations.GetLocationNameFromId(item.Location);
                    var itemName = session.Items.GetItemName(item.Item);

                    PlaceItem(locationName, itemName, item.Item);
                }
            }

            // TODO: Remove this code which removes '0'. Must be present due to broken server logic, but I need to get to work on client.
            var locations = new List<long>(session.Locations.AllLocations);
            locations.Remove(0);
            session.Locations.ScoutLocationsAsync(ScoutCallback, locations.ToArray());
        }

        public void PlaceItem(string location, string name, int apLocationId)
        {
            AbstractLocation loc = Finder.GetLocation(location);
            // TODO: remove this when logic has properly been imported and AP data isn't corrupt.
            if (loc == null)
            {
                return;
            }

            AbstractPlacement pmt = loc.Wrap();
            AbstractItem item = Finder.GetItem(name);

            // If item doesn't belong to Hollow Knight, then it is a remote item for another game.
            if (item == null)
            {
                item = new ArchipelagoItem(name, apLocationId);
            }
            pmt.Add(item);

            ItemChangerMod.AddPlacements(pmt.Yield());
        }

        private void CreateVanillaItemPlacements()
        {
            var allItems = Finder.GetFullItemList().Where(kvp => kvp.Value is not CustomSkillItem).ToDictionary(x => x.Key, x => x.Value);
            foreach (var kvp in allItems)
            {
                var name = kvp.Key;
                var item = kvp.Value;

                var apLocation = new ArchipelagoLocation(name);
                var placement = apLocation.Wrap();
                placement.Add(item);
                item.UIDef = new MsgUIDef()
                {
                    name = new BoxedString(item.UIDef.GetPreviewName()),
                    shopDesc = new BoxedString(item.UIDef.GetShopDesc()),
                    sprite = new BoxedSprite(item.UIDef.GetSprite())
                };
                InteropTag tag = placement.AddTag<InteropTag>();
                tag.Message = "RecentItems";
                tag.Properties["DisplaySource"] = name;

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