using Archipelago.HollowKnight.IC;
using Archipelago.HollowKnight.IC.Modules;
using Archipelago.HollowKnight.IC.RM;
using Archipelago.HollowKnight.SlotDataModel;
using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Models;
using ItemChanger;
using ItemChanger.Extensions;
using ItemChanger.Modules;
using ItemChanger.Placements;
using ItemChanger.Tags;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Archipelago.HollowKnight
{
    /// <summary>
    /// Tracks state only required during initial randomization
    /// </summary>
    internal class ArchipelagoRandomizer
    {
        /// <summary>
        /// Randomized charm notch costs as stored in slot data.
        /// </summary>
        public List<int> NotchCosts { get; private set; }

        /// <summary>
        /// Tracks created placements and their associated locations during randomization.
        /// </summary>
        public Dictionary<string, AbstractPlacement> placements = new();

        /// <summary>
        /// Seeded RNG for clientside randomization.
        /// </summary>
        public readonly Random Random;

        /// <summary>
        /// Factory for IC item creation
        /// </summary>
        public readonly ItemFactory itemFactory;

        /// <summary>
        /// Factory for IC cost creation
        /// </summary>
        public readonly CostFactory costFactory;

        private readonly SlotData SlotData;
        private ArchipelagoSession Session => ArchipelagoMod.Instance.session;
        private ArchipelagoMod Instance => ArchipelagoMod.Instance;

        public ArchipelagoRandomizer(SlotData slotData)
        {
            SlotData = slotData;
            Random = new Random(slotData.Seed);
            itemFactory = new ItemFactory();
            costFactory = new CostFactory(slotData.LocationCosts);
            NotchCosts = slotData.NotchCosts;

            ArchipelagoMod.Instance.Log("Initializing ArchipelagoRandomizer with slot data: " + JsonConvert.SerializeObject(SlotData));
        }

        public void Randomize()
        {
            ArchipelagoSession session = Session;
            ItemChangerMod.CreateSettingsProfile();
            if (SlotData.Options.StartLocationName is string start)
            {
                if (IC.RM.StartDef.Lookup.TryGetValue(start, out IC.RM.StartDef def))
                {
                    ItemChangerMod.ChangeStartGame(def.ToItemChangerStartDef());
                    ArchipelagoMod.Instance.Log($"Set start to {start}");
                }
                else
                {
                    ArchipelagoMod.Instance.LogError($"Unsupported start location {start}, starting in King's Pass");
                }
            }

            AddItemChangerModules();
            AddHelperPlatforms();
            ApplyTransitionOverrides();

            ApplyCharmCosts();

            // Initialize shop locations in case they end up with zero items placed.
            AbstractLocation location;
            AbstractPlacement pmt;

            string[] shops = [
                LocationNames.Sly, LocationNames.Sly_Key, LocationNames.Iselda,
                LocationNames.Salubra, LocationNames.Leg_Eater, LocationNames.Grubfather,
                LocationNames.Seer
            ];
            foreach (string name in shops)
            {
                location = Finder.GetLocation(name);
                placements[name] = pmt = location.Wrap();

                pmt.AddTag<ArchipelagoPlacementTag>();

                if (pmt is ShopPlacement shop)
                {
                    shop.defaultShopItems = DefaultShopItems.IseldaMapPins
                        | DefaultShopItems.IseldaMapMarkers
                        | DefaultShopItems.LegEaterRepair;
                    if (SlotData.Options.AddUnshuffledLocations)
                    {
                        // AP will add the default items on our behalf
                        continue;
                    }

                    if (!SlotData.Options.RandomizeCharms)
                    {
                        shop.defaultShopItems |= DefaultShopItems.SlyCharms
                            | DefaultShopItems.SlyKeyCharms
                            | DefaultShopItems.IseldaCharms
                            | DefaultShopItems.SalubraCharms
                            | DefaultShopItems.LegEaterCharms;
                    }
                    if (!SlotData.Options.RandomizeMaps)
                    {
                        shop.defaultShopItems |= DefaultShopItems.IseldaMaps
                            | DefaultShopItems.IseldaQuill;
                    }
                    if (!SlotData.Options.RandomizeCharmNotches)
                    {
                        shop.defaultShopItems |= DefaultShopItems.SalubraNotches
                            | DefaultShopItems.SalubraBlessing;
                    }
                    if (!SlotData.Options.RandomizeKeys)
                    {
                        shop.defaultShopItems |= DefaultShopItems.SlySimpleKey
                            | DefaultShopItems.SlyLantern
                            | DefaultShopItems.SlyKeyElegantKey;
                    }
                    if (!SlotData.Options.RandomizeMaskShards)
                    {
                        shop.defaultShopItems |= DefaultShopItems.SlyMaskShards;
                    }
                    if (!SlotData.Options.RandomizeVesselFragments)
                    {
                        shop.defaultShopItems |= DefaultShopItems.SlyVesselFragments;
                    }
                    if (!SlotData.Options.RandomizeRancidEggs)
                    {
                        shop.defaultShopItems |= DefaultShopItems.SlyRancidEgg;
                    }
                }
                else if (name == LocationNames.Grubfather)
                {
                    DestroyGrubRewardTag t = pmt.AddTag<DestroyGrubRewardTag>();
                    t.destroyRewards = GrubfatherRewards.None;
                    if (SlotData.Options.AddUnshuffledLocations || SlotData.Options.RandomizeMaskShards)
                    {
                        t.destroyRewards |= GrubfatherRewards.MaskShard;
                    }
                    if (SlotData.Options.AddUnshuffledLocations || SlotData.Options.RandomizeCharms)
                    {
                        t.destroyRewards |= GrubfatherRewards.Grubsong | GrubfatherRewards.GrubberflysElegy;
                    }
                    if (SlotData.Options.AddUnshuffledLocations || SlotData.Options.RandomizeRancidEggs)
                    {
                        t.destroyRewards |= GrubfatherRewards.RancidEgg;
                    }
                    if (SlotData.Options.AddUnshuffledLocations || SlotData.Options.RandomizeRelics)
                    {
                        t.destroyRewards |= GrubfatherRewards.HallownestSeal | GrubfatherRewards.KingsIdol;
                    }
                    if (SlotData.Options.AddUnshuffledLocations || SlotData.Options.RandomizePaleOre)
                    {
                        t.destroyRewards |= GrubfatherRewards.PaleOre;
                    }
                }
                else if (name == LocationNames.Seer)
                {
                    DestroySeerRewardTag t = pmt.AddTag<DestroySeerRewardTag>();
                    t.destroyRewards = SeerRewards.None;
                    if (SlotData.Options.AddUnshuffledLocations || SlotData.Options.RandomizeRelics)
                    {
                        t.destroyRewards |= SeerRewards.HallownestSeal | SeerRewards.ArcaneEgg;
                    }
                    if (SlotData.Options.AddUnshuffledLocations || SlotData.Options.RandomizePaleOre)
                    {
                        t.destroyRewards |= SeerRewards.PaleOre;
                    }
                    if (SlotData.Options.AddUnshuffledLocations || SlotData.Options.RandomizeCharms)
                    {
                        t.destroyRewards |= SeerRewards.DreamWielder;
                    }
                    if (SlotData.Options.AddUnshuffledLocations || SlotData.Options.RandomizeVesselFragments)
                    {
                        t.destroyRewards |= SeerRewards.VesselFragment;
                    }
                    if (SlotData.Options.AddUnshuffledLocations || SlotData.Options.RandomizeSkills)
                    {
                        t.destroyRewards |= SeerRewards.DreamGate | SeerRewards.AwokenDreamNail;
                    }
                    if (SlotData.Options.AddUnshuffledLocations || SlotData.Options.RandomizeMaskShards)
                    {
                        t.destroyRewards |= SeerRewards.MaskShard;
                    }
                }
            }

            Task<Dictionary<long, ScoutedItemInfo>> scoutTask = session.Locations
                .ScoutLocationsAsync(session.Locations.AllLocations.ToArray());
            scoutTask.Wait();

            Dictionary<long, ScoutedItemInfo> scoutResult = scoutTask.Result;
            foreach (KeyValuePair<long, ScoutedItemInfo> scout in scoutResult)
            {
                long id = scout.Key;
                ScoutedItemInfo item = scout.Value;
                string itemName = item.ItemName ?? $"?Item {item.ItemId}";
                PlaceItem(item.LocationName, itemName, item);
            }
            ItemChangerMod.AddPlacements(placements.Values);

        }

        private void AddItemChangerModules()
        {
            ItemChangerMod.Modules.Add<DupeHandlingModule>();
            ItemChangerMod.Modules.Add<ItemNetworkingModule>();
            ItemChangerMod.Modules.Add<GiftingModule>();
            ItemChangerMod.Modules.Add<GoalModule>();
            ItemChangerMod.Modules.Add<CompletionPercentOverride>();
            ItemChangerMod.Modules.Add<HintTracker>();
            ItemChangerMod.Modules.Add<RepositionShadeModule>();
            ItemChangerMod.Modules.Add<BenchSyncModule>();
            ItemChangerMod.Modules.Add<StartLocationSceneEditsModule>();

            if (SlotData.Options.DeathLink)
            {
                ItemChangerMod.Modules.Add<DeathLinkModule>();
            }

            if (SlotData.Options.RandomizeElevatorPass)
            {
                ItemChangerMod.Modules.Add<ElevatorPass>();
            }

            if (SlotData.Options.RandomizeFocus)
            {
                ItemChangerMod.Modules.Add<FocusSkill>();
            }

            if (SlotData.Options.RandomizeSwim)
            {
                ItemChangerMod.Modules.Add<SwimSkill>();
            }

            if (SlotData.Options.SplitMothwingCloak)
            {
                ItemChangerMod.Modules.Add<SplitCloak>();
            }

            if (SlotData.Options.SplitMantisClaw)
            {
                ItemChangerMod.Modules.Add<SplitClaw>();
            }

            if (SlotData.Options.SplitCrystalHeart)
            {
                ItemChangerMod.Modules.Add<SplitSuperdash>();
            }

            if (SlotData.Options.Slopeballs)
            {
                ItemChangerMod.Modules.Add<ToggleableFireballUpgrade>();
            }

            if (SlotData.Options.EntranceRandoType != EntranceRandoType.None)
            {
                HashSet<string> targetNames = SlotData.EntrancePairs != null ? [.. SlotData.EntrancePairs.Values] : [];
                if (targetNames.Contains($"{SceneNames.White_Palace_18}[top1]")
                    || targetNames.Contains($"{SceneNames.White_Palace_17}[right1]")
                    || targetNames.Contains($"{SceneNames.White_Palace_19}[top1]"))
                {
                    ItemChangerMod.Modules.Add<ItemChanger.Modules.ReversePathOfPainSaw>();
                }
                ItemChangerMod.Modules.Add<DisablePalaceMidWarp>();
                ItemChangerMod.Modules.Add<RemoveInfectedBlockades>();
            }
        }

        private void AddHelperPlatforms()
        {
            HelperPlatformBuilder.AddConveniencePlatforms(SlotData);
            HelperPlatformBuilder.AddStartLocationRequiredPlatforms(SlotData.Options);
        }

        private void ApplyTransitionOverrides()
        {
            Dictionary<string, string> entrances = SlotData.EntrancePairs;

            if (entrances == null || entrances.Count == 0)
            {
                ArchipelagoMod.Instance.Log("[ER] No entrance pairings provided");
                return;
            }

            ArchipelagoMod.Instance.Log($"[ER] EntranceRandomizerModule initializing with {entrances.Count} entrances");

            foreach (KeyValuePair<string, string> pair in entrances)
            {
                if (pair.Key == null || pair.Value == null)
                {
                    continue;
                }

                try
                {
                    Transition sourceTransition = ParseTransition(pair.Key);
                    Transition targetTransition = ParseTransition(pair.Value);

                    ItemChangerMod.AddTransitionOverride(sourceTransition, targetTransition);

                    ArchipelagoMod.Instance.LogDebug(
                        $"[ER] Overrode transition: {pair.Key} -> {pair.Value}"
                    );
                }
                catch (Exception ex)
                {
                    ArchipelagoMod.Instance.LogWarn(
                        $"[ER] Failed to parse transition pair {pair.Key} -> {pair.Value}: {ex.Message}"
                    );
                }
            }

            ArchipelagoMod.Instance.Log($"[ER] Finished overriding transitions");
        }

        private void ApplyCharmCosts()
        {
            bool isNotchCostsRandomizedOrPlando = false;
            for (int i = 0; i < NotchCosts.Count; i++)
            {
                if (PlayerData.instance.GetInt($"charmCost_{i + 1}") != NotchCosts[i])
                {
                    isNotchCostsRandomizedOrPlando = true;
                    break;
                }
            }
            if (!isNotchCostsRandomizedOrPlando)
            {
                return;
            }

            ItemChangerMod.Modules.Add<NotchCostUI>();
            ItemChangerMod.Modules.Add<ZeroCostCharmEquip>();
            PlayerDataEditModule playerDataEditModule = ItemChangerMod.Modules.GetOrAdd<PlayerDataEditModule>();
            Instance.LogDebug(playerDataEditModule);
            for (int i = 0; i < NotchCosts.Count; i++)
            {
                playerDataEditModule.AddPDEdit($"charmCost_{i + 1}", NotchCosts[i]);
            }
        }

        public void PlaceItem(string location, string name, ScoutedItemInfo itemInfo)
        {
            Instance.LogDebug($"[PlaceItem] Placing item {name} into {location} with ID {itemInfo.ItemId}");

            string originalLocation = string.Copy(location);
            location = StripShopSuffix(location);
            // IC does not like placements at these locations if there's also a location at the lore tablet, it renders the lore tablet inoperable.
            // But we can have multiple placements at the same location, so do this workaround.  (Rando4 does something similar per its README)
            if (SlotData.Options.RandomizeLoreTablets)
            {
                switch (location)
                {
                    case LocationNames.Focus:
                        location = LocationNames.Lore_Tablet_Kings_Pass_Focus;
                        break;
                    case LocationNames.World_Sense:
                        location = LocationNames.Lore_Tablet_World_Sense;
                        break;
                        // no default
                }
            }

            AbstractLocation loc = Finder.GetLocation(location);
            if (loc == null)
            {
                Instance.LogDebug($"[PlaceItem] Location was null: Name: {location}.");
                return;
            }

            bool isMyItem = itemInfo.IsReceiverRelatedToActivePlayer;
            string recipientName = null;
            if (!isMyItem)
            {
                recipientName = Session.Players.GetPlayerName(itemInfo.Player);
            }

            AbstractPlacement pmt = placements.GetOrDefault(location);
            if (pmt == null)
            {
                pmt = loc.Wrap();
                pmt.AddTag<ArchipelagoPlacementTag>();
                placements[location] = pmt;
            }

            AbstractItem item;
            if (isMyItem)
            {
                item = itemFactory.CreateMyItem(name, itemInfo);
            }
            else
            {
                item = itemFactory.CreateRemoteItem(pmt, recipientName, name, itemInfo);
            }

            pmt.Add(item);
            costFactory.ApplyCost(pmt, item, originalLocation);
        }

        private string StripShopSuffix(string location)
        {
            if (string.IsNullOrEmpty(location))
            {
                return null;
            }

            string[] names =
            [
                LocationNames.Sly_Key, LocationNames.Sly, LocationNames.Iselda, LocationNames.Salubra,
                LocationNames.Leg_Eater, LocationNames.Egg_Shop, LocationNames.Seer, LocationNames.Grubfather
            ];

            foreach (string name in names)
            {
                if (location.StartsWith(name))
                {
                    return location.Substring(0, name.Length);
                }
            }

            return location;
        }

        private Transition ParseTransition(string transitionString)
        {
            int bracketStart = transitionString.IndexOf('[');
            int bracketEnd = transitionString.IndexOf(']');

            if (bracketStart < 0 || bracketEnd < 0)
            {
                throw new ArgumentException($"Invalid transition format: {transitionString}");
            }

            string sceneName = transitionString[..bracketStart].Trim();
            string doorName = transitionString[(bracketStart + 1)..bracketEnd].Trim();

            return new Transition(sceneName, doorName);
        }
    }
}