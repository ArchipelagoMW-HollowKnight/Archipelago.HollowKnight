using Archipelago.HollowKnight.IC;
using Archipelago.HollowKnight.IC.Modules;
using Archipelago.HollowKnight.IC.RM;
using Archipelago.HollowKnight.SlotData;
using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Models;
using Archipelago.MultiClient.Net.Packets;
using ItemChanger;
using ItemChanger.Extensions;
using ItemChanger.Modules;
using ItemChanger.Placements;
using ItemChanger.Tags;
using System;
using System.Collections.Generic;

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

        private SlotOptions SlotOptions => Archipelago.Instance.SlotOptions;
        private ArchipelagoSession Session => Archipelago.Instance.session;
        private Archipelago Instance => Archipelago.Instance;

        public ArchipelagoRandomizer(Dictionary<string, object> slotData)
        {
            Random = new Random(Convert.ToInt32((long) slotData["seed"]));
            itemFactory = new ItemFactory();
            costFactory = new CostFactory(SlotDataExtract.ExtractLocationCostsFromSlotData(slotData["location_costs"]));
            NotchCosts = SlotDataExtract.ExtractArrayFromSlotData<List<int>>(slotData["notch_costs"]);
        }

        public void Randomize()
        {
            ArchipelagoSession session = Session;
            ItemChangerMod.CreateSettingsProfile();
            // Add IC modules as needed
            // FUTURE: If Entrance rando, disable palace midwarp and some logical blockers
            // if (Entrance Rando Is Enabled) {
            //     ItemChangerMod.Modules.Add<ItemChanger.Modules.DisablePalaceMidWarp>();
            //     ItemChangerMod.Modules.Add<ItemChanger.Modules.RemoveInfectedBlockades>();
            // }

            AddItemChangerModules();
            AddHelperPlatforms();

            ApplyCharmCosts();

            // Initialize shop locations in case they end up with zero items placed.
            AbstractLocation location;
            AbstractPlacement pmt;

            string[] shops = new string[]
            {
                LocationNames.Sly, LocationNames.Sly_Key, LocationNames.Iselda,
                LocationNames.Salubra, LocationNames.Leg_Eater, LocationNames.Grubfather,
                LocationNames.Seer
            };
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
                    if (!SlotOptions.RandomizeCharms)
                    {
                        shop.defaultShopItems |= DefaultShopItems.SlyCharms 
                            | DefaultShopItems.SlyKeyCharms
                            | DefaultShopItems.IseldaCharms
                            | DefaultShopItems.SalubraCharms
                            | DefaultShopItems.LegEaterCharms;
                    }
                    if (!SlotOptions.RandomizeMaps)
                    {
                        shop.defaultShopItems |= DefaultShopItems.IseldaMaps
                            | DefaultShopItems.IseldaQuill;
                    }
                    if (!SlotOptions.RandomizeCharmNotches)
                    {
                        shop.defaultShopItems |= DefaultShopItems.SalubraNotches
                            | DefaultShopItems.SalubraBlessing;
                    }
                    if (!SlotOptions.RandomizeKeys)
                    {
                        shop.defaultShopItems |= DefaultShopItems.SlySimpleKey
                            | DefaultShopItems.SlyLantern
                            | DefaultShopItems.SlyKeyElegantKey;
                    }
                    if (!SlotOptions.RandomizeMaskShards)
                    {
                        shop.defaultShopItems |= DefaultShopItems.SlyMaskShards;
                    }
                    if (!SlotOptions.RandomizeVesselFragments)
                    {
                        shop.defaultShopItems |= DefaultShopItems.SlyVesselFragments;
                    }
                    if (!SlotOptions.RandomizeRancidEggs)
                    {
                        shop.defaultShopItems |= DefaultShopItems.SlyRancidEgg;
                    }
                }
                else if (name == LocationNames.Grubfather)
                {
                    DestroyGrubRewardTag t = pmt.AddTag<DestroyGrubRewardTag>();
                    t.destroyRewards = GrubfatherRewards.None;
                    if (SlotOptions.RandomizeMaskShards)
                    {
                        t.destroyRewards |= GrubfatherRewards.MaskShard;
                    }
                    if (SlotOptions.RandomizeCharms)
                    {
                        t.destroyRewards |= GrubfatherRewards.Grubsong | GrubfatherRewards.GrubberflysElegy;
                    }
                    if (SlotOptions.RandomizeRancidEggs)
                    {
                        t.destroyRewards |= GrubfatherRewards.RancidEgg;
                    }
                    if (SlotOptions.RandomizeRelics)
                    {
                        t.destroyRewards |= GrubfatherRewards.HallownestSeal | GrubfatherRewards.KingsIdol;
                    }
                    if (SlotOptions.RandomizePaleOre)
                    {
                        t.destroyRewards |= GrubfatherRewards.PaleOre;
                    }
                }
                else if (name == LocationNames.Seer)
                {
                    DestroySeerRewardTag t = pmt.AddTag<DestroySeerRewardTag>();
                    t.destroyRewards = SeerRewards.None;
                    if (SlotOptions.RandomizeRelics)
                    {
                        t.destroyRewards |= SeerRewards.HallownestSeal | SeerRewards.ArcaneEgg;
                    }
                    if (SlotOptions.RandomizePaleOre)
                    {
                        t.destroyRewards |= SeerRewards.PaleOre;
                    }
                    if (SlotOptions.RandomizeCharms)
                    {
                        t.destroyRewards |= SeerRewards.DreamWielder;
                    }
                    if (SlotOptions.RandomizeVesselFragments)
                    {
                        t.destroyRewards |= SeerRewards.VesselFragment;
                    }
                    if (SlotOptions.RandomizeSkills)
                    {
                        t.destroyRewards |= SeerRewards.DreamGate | SeerRewards.AwokenDreamNail;
                    }
                    if (SlotOptions.RandomizeMaskShards) 
                    {
                        t.destroyRewards |= SeerRewards.MaskShard;
                    }
                }
            }

            // Scout all locations
            void ScoutCallback(LocationInfoPacket packet)
            {
                foreach (NetworkItem item in packet.Locations)
                {
                    string locationName = session.Locations.GetLocationNameFromId(item.Location);
                    string itemName = session.Items.GetItemName(item.Item) ?? $"?Item {item.Item}?";

                    PlaceItem(locationName, itemName, item);
                }

                ItemChangerMod.AddPlacements(placements.Values);
            }

            List<long> locations = new(session.Locations.AllLocations);
            session.Locations.ScoutLocationsAsync(locations.ToArray())
                             .ContinueWith(task =>
                             {
                                 LocationInfoPacket packet = task.Result;
                                 ScoutCallback(packet);
                             }).Wait();
        }

        private void AddItemChangerModules()
        {
            ItemChangerMod.Modules.Add<ItemNetworkingModule>();
            ItemChangerMod.Modules.Add<GoalModule>();
            ItemChangerMod.Modules.Add<CompletionPercentOverride>();
            ItemChangerMod.Modules.Add<HintTracker>();
            ItemChangerMod.Modules.Add<RepositionShadeModule>();

            if (SlotOptions.DeathLink)
            {
                ItemChangerMod.Modules.Add<DeathLinkModule>();
            }

            if (SlotOptions.RandomizeElevatorPass)
            {
                ItemChangerMod.Modules.Add<ElevatorPass>();
            }

            if (SlotOptions.RandomizeFocus)
            {
                ItemChangerMod.Modules.Add<FocusSkill>();
            }

            if (SlotOptions.RandomizeSwim)
            {
                ItemChangerMod.Modules.Add<SwimSkill>();
            }

            if (SlotOptions.SplitMothwingCloak)
            {
                ItemChangerMod.Modules.Add<SplitCloak>();
            }

            if (SlotOptions.SplitMantisClaw)
            {
                ItemChangerMod.Modules.Add<SplitClaw>();
            }

            if (SlotOptions.SplitCrystalHeart)
            {
                ItemChangerMod.Modules.Add<SplitSuperdash>();
            }
        }

        private void AddHelperPlatforms()
        {
            HelperPlatformBuilder.AddConveniencePlatforms(SlotOptions);
            HelperPlatformBuilder.AddStartLocationRequiredPlatforms(SlotOptions);
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

        public void PlaceItem(string location, string name, NetworkItem netItem)
        {
            Instance.LogDebug($"[PlaceItem] Placing item {name} into {location} with ID {netItem.Item}");

            string originalLocation = string.Copy(location);
            location = StripShopSuffix(location);
            // IC does not like placements at these locations if there's also a location at the lore tablet, it renders the lore tablet inoperable.
            // But we can have multiple placements at the same location, so do this workaround.  (Rando4 does something similar per its README)
            if (SlotOptions.RandomizeLoreTablets)
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
            // below is needed for back compat with 0.4.4
            else if (location == LocationNames.Lore_Tablet_World_Sense)
            {
                location = LocationNames.World_Sense;
            }
            else if (SlotOptions.RandomizeFocus && location == LocationNames.Lore_Tablet_Kings_Pass_Focus)
            {
                location = LocationNames.Focus;
            }

            AbstractLocation loc = Finder.GetLocation(location);
            if (loc == null)
            {
                Instance.LogDebug($"[PlaceItem] Location was null: Name: {location}.");
                return;
            }

            bool isMyItem = GroupUtil.WillItemRouteToMe(netItem.Player);
            string recipientName = null;
            if (!isMyItem)
            {
                recipientName = Session.Players.GetPlayerName(netItem.Player);
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
                item = itemFactory.CreateMyItem(name, netItem);
            }
            else
            {
                item = itemFactory.CreateRemoteItem(pmt, recipientName, name, netItem);
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

            string[] names = new[]
            {
                LocationNames.Sly_Key, LocationNames.Sly, LocationNames.Iselda, LocationNames.Salubra,
                LocationNames.Leg_Eater, LocationNames.Egg_Shop, LocationNames.Seer, LocationNames.Grubfather
            };

            foreach (string name in names)
            {
                if (location.StartsWith(name))
                {
                    return location.Substring(0, name.Length);
                }
            }

            return location;
        }
    }
}