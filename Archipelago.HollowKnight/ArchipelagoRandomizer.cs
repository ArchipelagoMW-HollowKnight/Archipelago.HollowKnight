using System;
using System.Collections.Generic;
using System.Linq;
using Archipelago.HollowKnight.IC;
using Archipelago.HollowKnight.Placements;
using Archipelago.HollowKnight.SlotData;
using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Models;
using Archipelago.MultiClient.Net.Packets;
using ItemChanger;
using ItemChanger.Extensions;
using ItemChanger.Items;
using ItemChanger.Placements;
using ItemChanger.Tags;

namespace Archipelago.HollowKnight
{
    /// <summary>
    /// Tracks state only required during initial randomization
    /// </summary>
    internal class ArchipelagoRandomizer
    {
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
        /// Randomized charm notch costs as stored in slot data.
        /// </summary>
        public List<int> NotchCosts { get; private set; }
        /// <summary>
        /// Tracks created placements and their associated locations during randomization.
        /// </summary>
        public Dictionary<string, AbstractPlacement> placements = new();

        public Dictionary<string, Dictionary<string, int>> LocationCosts = new();
        /// <summary>
        /// Seeded RNG for clientside randomization.
        /// </summary>
        public readonly Random Random;

        private SlotOptions SlotOptions => Archipelago.Instance.SlotOptions;
        private ArchipelagoSession Session => Archipelago.Instance.session;
        private Archipelago Instance => Archipelago.Instance;

        private List<IPlacementHandler> placementHandlers;

        public ArchipelagoRandomizer(Dictionary<string, object> slotData)
        {
            Random = new Random(Convert.ToInt32((long)slotData["seed"]));
            NotchCosts = SlotDataExtract.ExtractArrayFromSlotData<List<int>>(slotData["notch_costs"]);

            if (slotData.ContainsKey("location_costs"))
            {
                LocationCosts = SlotDataExtract.ExtractLocationCostsFromSlotData(slotData["location_costs"]);
                GrubfatherCosts = null;
                SeerCosts = null;
                EggCosts = null;
                SalubraCharmCosts = null;
                placementHandlers = null;
            }
            else
            {
                LocationCosts = null;
                GrubfatherCosts = SlotDataExtract.ExtractObjectFromSlotData<Dictionary<string, int>>(slotData["Grub_costs"]);
                SeerCosts = SlotDataExtract.ExtractObjectFromSlotData<Dictionary<string, int>>(slotData["Essence_costs"]);
                EggCosts = SlotDataExtract.ExtractObjectFromSlotData<Dictionary<string, int>>(slotData["Egg_costs"]);
                SalubraCharmCosts = SlotDataExtract.ExtractObjectFromSlotData<Dictionary<string, int>>(slotData["Charm_costs"]);
                placementHandlers = new List<IPlacementHandler>()
                {
                    new ShopPlacementHandler(Random),
                    new GrubfatherPlacementHandler(GrubfatherCosts),
                    new SeerPlacementHandler(SeerCosts),
                    new EggShopPlacementHandler(EggCosts),
                    new SalubraCharmShopPlacementHandler(SalubraCharmCosts, Random)
                };
            }
            Archipelago.Instance.LogDebug(LocationCosts);
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
                    shop.defaultShopItems = DefaultShopItems.IseldaMapPins | DefaultShopItems.IseldaMapMarkers | DefaultShopItems.LegEaterRepair;
                }
                else if (name == LocationNames.Grubfather)
                {
                    pmt.AddTag<DestroyGrubRewardTag>().destroyRewards = GrubfatherRewards.AllNonGeo;
                }
                else if (name == LocationNames.Seer)
                {
                    pmt.AddTag<DestroySeerRewardTag>().destroyRewards = SeerRewards.All & ~SeerRewards.GladeDoor & ~SeerRewards.Ascension; ;
                }
            }

            // Scout all locations
            void ScoutCallback(LocationInfoPacket packet)
            {
                MenuChanger.ThreadSupport.BeginInvoke(() =>
                {
                    foreach (var item in packet.Locations)
                    {
                        var locationName = session.Locations.GetLocationNameFromId(item.Location);
                        var itemName = session.Items.GetItemName(item.Item) ?? $"?Item {item.Item}?";

                        PlaceItem(locationName, itemName, item);
                    }
                    ItemChangerMod.AddPlacements(placements.Values);
                });
            }

            var locations = new List<long>(session.Locations.AllLocations);
            session.Locations.ScoutLocationsAsync(ScoutCallback, locations.ToArray());
        }

        private void AddItemChangerModules()
        {
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

            string recipientName = null;
            if (netItem.Player != slot)
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
            InteropTag tag;
            if (Finder.ItemNames.Contains(name))
            {
                // Items from Hollow Knight.
                item = Finder.GetItem(name);  // This is already a clone per what I can tell from IC source
                if (recipientName != null)
                {
                    tag = item.AddTag<InteropTag>();
                    tag.Message = "RecentItems";
                    tag.Properties["DisplayMessage"] = $"{ArchipelagoUIDef.GetSentItemName(item)}\nsent to {recipientName}.";
                    item.UIDef = ArchipelagoUIDef.CreateForSentItem(item, recipientName);

                    if (item is SoulItem soulItem)
                    {
                        soulItem.soul = 0;
                    }
                }
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

            if (LocationCosts == null)
            {
                // Backwards compatible placement logic
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
                return;
            }

            pmt.Add(item);
            if (LocationCosts.ContainsKey(originalLocation))
            {
                // New-style placement logic with cost overrides
                List<Cost> costs = new();
                foreach (KeyValuePair<string, int> entry in LocationCosts[originalLocation])
                {
                    switch (entry.Key)
                    {
                        case "GEO":
                            costs.Add(Cost.NewGeoCost(entry.Value));
                            break;
                        case "ESSENCE":
                            costs.Add(Cost.NewEssenceCost(entry.Value));
                            break;
                        case "GRUBS":
                            costs.Add(Cost.NewGrubCost(entry.Value));
                            break;
                        case "CHARMS":
                            costs.Add(new PDIntCost(
                                entry.Value, nameof(PlayerData.charmsOwned),
                                $"Acquire {entry.Value} {((entry.Value == 1) ? "charm" : "charms")}"
                            ));
                            break;
                        case "RANCIDEGGS":
                            costs.Add(new ItemChanger.Modules.CumulativeRancidEggCost(entry.Value));
                            break;
                        default:
                            Archipelago.Instance.LogWarn($"Encountered UNKNOWN currency type {entry.Key} at location {originalLocation}!");
                            break;
                    }
                }

                if (costs.Count == 0)
                {
                    Archipelago.Instance.LogWarn($"Found zero cost types when handling placement at location {originalLocation}!");
                    return;
                }

                var costTag = item.AddTag<CostTag>();
                if (costs.Count == 1)
                {
                    costTag.Cost = costs[0];
                }
                else
                {
                    costTag.Cost = new MultiCost(costs);
                }

                if (pmt is ISingleCostPlacement scp)
                {
                    scp.Cost = costTag.Cost;
                }
            }
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
