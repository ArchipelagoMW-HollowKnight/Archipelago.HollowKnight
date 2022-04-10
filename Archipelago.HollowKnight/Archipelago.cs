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
    public class Archipelago : Mod, ILocalSettings<ConnectionDetails>
    {
        public enum Goal
        {
            Any = 0,
            HollowKnight = 1,
            SealedSiblings = 2,
            Radiance = 3,
            Godhome = 4,
            MAX = Godhome
        }

        public static readonly Dictionary<Goal, string> GoalNames = new Dictionary<Goal, string>()
        {
            [Goal.Any] = "Beat the Game",
            [Goal.HollowKnight] = "The Hollow Knight",
            [Goal.SealedSiblings] = "Sealed Siblings",
            [Goal.Radiance] = "Dream No More",
            [Goal.Godhome] = "Embrace the Void"
        };

        public static readonly Dictionary<Goal, string> GoalDescriptions = new Dictionary<Goal, string>()
        {
            [Goal.Any] = "Complete Hollow Knight with any ending.",
            [Goal.HollowKnight] = "Defeat The Hollow Knight<br>or any other ending with 3 dreamers.",
            [Goal.SealedSiblings] = "Complete the Sealed Siblings ending<br>or any other ending with Void Heart equipped.",
            [Goal.Radiance] = "Defeat The Radiance or Absolute Radiance<br>after obtaining Void Heart and 3 dreamers.",
            [Goal.Godhome] = "Defeat Absolute Radiance<br>at the end of Pantheon 5."
        };


        private readonly Version ArchipelagoProtocolVersion = new Version(0, 3, 0);

        public static Archipelago Instance;
        public SlotOptions SlotOptions { get; set; }
        public bool ArchipelagoEnabled { get; set; }
        public Dictionary<string, int> GrubfatherCosts { get; private set; }
        public Dictionary<string, int> SeerCosts { get; private set; }
        public Dictionary<string, int> EggCosts { get; private set; }
        public Dictionary<string, int> SalubraCharmCosts { get; private set; }
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
            // On.GameCompletionScreen.Start += OnGameComplete;
            ItemChanger.Events.OnSceneChange += Events_OnSceneChange;
            // ModHooks.SceneChanged += ModHooks_SceneChanged;
            Log("Initialized");
        }

        private void Events_OnSceneChange(UnityEngine.SceneManagement.Scene scene)
        {
            string sceneName = scene.name;
            LogDebug($"Detected change of scene to: {sceneName}.");
            if (!ArchipelagoEnabled) return;

            bool acquiredVoidHeart = PlayerData.instance.GetInt(nameof(PlayerData.royalCharmState)) == 4;
            bool equippedVoidHeart = acquiredVoidHeart && PlayerData.instance.GetBool(nameof(PlayerData.equippedCharm_36));
            bool threeDreamers = PlayerData.instance.GetInt(nameof(PlayerData.guardiansDefeated)) >= 3;
            LogDebug($"VH Acquired: {acquiredVoidHeart}; VH Equipped: {equippedVoidHeart}; Three Dreamers: {threeDreamers}");

            bool victory = false;
            Goal goal = SlotOptions.Goal;

            switch (sceneName)
            {
                case SceneNames.Cinematic_Ending_A:  // THK ending
                    victory = goal == Goal.Any || goal == Goal.HollowKnight;
                    break;
                case SceneNames.Cinematic_Ending_B:  // Sealed Siblings ending
                    victory = goal == Goal.Any || goal == Goal.HollowKnight || goal == Goal.SealedSiblings;
                    break;
                case SceneNames.Cinematic_Ending_C:  // Radiance ending
                    victory = goal == Goal.Any || goal == Goal.HollowKnight || goal == Goal.SealedSiblings || goal == Goal.Radiance;
                    break;
                case "Cinematic_Ending_D":  // This isn't listed in SceneNames; it's possible it's unused.  Godhome ending  (TODO: Verify this)
                case SceneNames.Cinematic_Ending_E:  // ... with flower quest?  (TODO: Verify this)
                    victory = (
                        (goal == Goal.Any || goal == Goal.Godhome)
                        || (threeDreamers && (
                            (goal == Goal.HollowKnight)
                            || (acquiredVoidHeart && (goal == Goal.SealedSiblings || goal == Goal.Radiance))
                        ))
                    );
                    break;
                default:
                    return;
            }

            if (!victory) return;
            LogDebug("Declaring victory!");
            session.Socket.SendPacket(new StatusUpdatePacket()
            {
                Status = ArchipelagoClientState.ClientGoal
            });
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

            ItemChangerMod.CreateSettingsProfile();

            ConnectToArchipelago();
            if (SlotOptions.RandomCharmCosts != -1)
            {
                RandomizeCharmCosts();
            }
            CreateItemPlacements();
            CreateVanillaItemPlacements();
            SetupLanguageEdits();

        }

        private void SetupLanguageEdits()
        {
            Goal goal = SlotOptions.Goal;
            if(goal < 0 || goal > Goal.MAX)
            {
                goal = Goal.Any;
            }
            string name = GoalNames[goal];
            string desc = GoalDescriptions[goal];
            ItemChanger.Events.AddLanguageEdit(new ItemChanger.LanguageKey("Prompts", "FOUNTAIN_PLAQUE_TOP"), (ref string s) => { s = "Your goal is"; });
            ItemChanger.Events.AddLanguageEdit(new ItemChanger.LanguageKey("Prompts", "FOUNTAIN_PLAQUE_MAIN"), (ref string s) => { s = name; });
            ItemChanger.Events.AddLanguageEdit(new ItemChanger.LanguageKey("Prompts", "FOUNTAIN_PLAQUE_DESC"), (ref string s) => { s = desc; });
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

                if(SlotOptions.Goal < 0 || SlotOptions.Goal > Goal.MAX)
                {
                    // TODO: Should probably yell about using an outdated client here.  I'm not certain the best way to do that, so just default it to ANY for now
                    // --Dewin
                    SlotOptions.Goal = Goal.Any;
                }
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
            ApSettings = details;
        }

        public ConnectionDetails OnSaveLocal()
        {
            return ApSettings;
        }
    }
}