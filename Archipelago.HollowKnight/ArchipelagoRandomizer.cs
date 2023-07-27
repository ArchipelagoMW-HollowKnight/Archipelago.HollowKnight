using Archipelago.HollowKnight.IC;
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
            var session = Session;
            ItemChangerMod.CreateSettingsProfile();
            // Add IC modules as needed
            // FUTURE: If Entrance rando, disable palace midwarp and some logical blockers
            // if (Entrance Rando Is Enabled) {
            //     ItemChangerMod.Modules.Add<ItemChanger.Modules.DisablePalaceMidWarp>();
            //     ItemChangerMod.Modules.Add<ItemChanger.Modules.RemoveInfectedBlockades>();
            // }

            AddItemChangerModules();

            if (SlotOptions.RandomCharmCosts != -1)
            {
                RandomizeCharmCosts();
            }

            // Initialize shop locations in case they end up with zero items placed.
            AbstractLocation location;
            AbstractPlacement pmt;

            var shops = new string[]
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
                    shop.defaultShopItems = DefaultShopItems.IseldaMapPins | DefaultShopItems.IseldaMapMarkers |
                                            DefaultShopItems.LegEaterRepair;
                }
                else if (name == LocationNames.Grubfather)
                {
                    pmt.AddTag<DestroyGrubRewardTag>().destroyRewards = GrubfatherRewards.AllNonGeo;
                }
                else if (name == LocationNames.Seer)
                {
                    pmt.AddTag<DestroySeerRewardTag>().destroyRewards =
                        SeerRewards.All & ~SeerRewards.GladeDoor & ~SeerRewards.Ascension;
                    ;
                }
            }

            // Scout all locations
            void ScoutCallback(LocationInfoPacket packet)
            {
                foreach (var item in packet.Locations)
                {
                    var locationName = session.Locations.GetLocationNameFromId(item.Location);
                    var itemName = session.Items.GetItemName(item.Item) ?? $"?Item {item.Item}?";

                    PlaceItem(locationName, itemName, item);
                }

                ItemChangerMod.AddPlacements(placements.Values);
            }

            var locations = new List<long>(session.Locations.AllLocations);
            session.Locations.ScoutLocationsAsync(locations.ToArray())
                             .ContinueWith(task =>
                             {
                                 var packet = task.Result;
                                 ScoutCallback(packet);
                             }).Wait();
        }

        private void AddItemChangerModules()
        {
            ItemChangerMod.Modules.Add<CompletionPercentOverride>();

            if (SlotOptions.RandomizeElevatorPass)
            {
                ItemChangerMod.Modules.Add<ItemChanger.Modules.ElevatorPass>();
            }

            if (SlotOptions.RandomizeFocus)
            {
                ItemChangerMod.Modules.Add<ItemChanger.Modules.FocusSkill>();
            }

            if (SlotOptions.RandomizeSwim)
            {
                ItemChangerMod.Modules.Add<ItemChanger.Modules.SwimSkill>();
            }

            if (SlotOptions.SplitMothwingCloak)
            {
                ItemChangerMod.Modules.Add<ItemChanger.Modules.SplitCloak>();
            }

            if (SlotOptions.SplitMantisClaw)
            {
                ItemChangerMod.Modules.Add<ItemChanger.Modules.SplitClaw>();
            }

            if (SlotOptions.SplitCrystalHeart)
            {
                ItemChangerMod.Modules.Add<ItemChanger.Modules.SplitSuperdash>();
            }
        }

        private void RandomizeCharmCosts()
        {
            ItemChangerMod.Modules.Add<ItemChanger.Modules.NotchCostUI>();
            ItemChangerMod.Modules.Add<ItemChanger.Modules.ZeroCostCharmEquip>();
            var playerDataEditModule = ItemChangerMod.Modules.GetOrAdd<ItemChanger.Modules.PlayerDataEditModule>();
            Instance.LogDebug(playerDataEditModule);
            for (int i = 0; i < NotchCosts.Count; i++)
            {
                playerDataEditModule.AddPDEdit($"charmCost_{i + 1}", NotchCosts[i]);
            }
        }

        public void PlaceItem(string location, string name, NetworkItem netItem)
        {
            var slot = Archipelago.Instance.Slot;
            Instance.LogDebug($"[PlaceItem] Placing item {name} into {location} with ID {netItem.Item}");

            var originalLocation = string.Copy(location);
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

            bool isMyItem = netItem.Player == slot;
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
                item = itemFactory.CreateRemoteItem(recipientName, name, netItem);
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
    }
}