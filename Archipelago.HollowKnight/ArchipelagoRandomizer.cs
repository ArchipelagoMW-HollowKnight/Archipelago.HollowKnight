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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        public Dictionary<AbstractLocation, AbstractPlacement> placements = new();

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
            Random = new System.Random(Convert.ToInt32((long)slotData["seed"]));
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
            //LocationCosts = SlotDataExtract.ExtractLocationCostsFromSlotData(slotData["location_costs"]);
            Archipelago.Instance.LogDebug(LocationCosts);
        }

        public void Randomize()
        {
            var session = Session;
            ItemChangerMod.CreateSettingsProfile();
            if (SlotOptions.RandomCharmCosts != -1)
            {
                RandomizeCharmCosts();
            }

            void ScoutCallback(LocationInfoPacket packet)
            {
                MenuChanger.ThreadSupport.BeginInvoke(() =>
                {
                    foreach (var item in packet.Locations.Reverse())  // Quick to insert items in reverse order to fix shop sorting for now.
                    {
                        var locationName = session.Locations.GetLocationNameFromId(item.Location);
                        var itemName = session.Items.GetItemName(item.Item);

                        PlaceItem(locationName, itemName, item);
                    }
                    ItemChangerMod.AddPlacements(placements.Values);
                });
            }

            var locations = new List<long>(session.Locations.AllLocations);
            session.Locations.ScoutLocationsAsync(ScoutCallback, locations.ToArray());
        
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

            AbstractPlacement pmt = placements.GetOrDefault(loc);
            if (pmt == null)
            {
                pmt = loc.Wrap();
                pmt.AddTag<ArchipelagoPlacementTag>();
                placements[loc] = pmt;
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
                    tag.Properties["DisplayMessage"] = $"{item.UIDef.GetPostviewName()}\nsent to {recipientName}.";
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
                Cost cost;
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
                                $"Acquire {entry.Value} total {((entry.Value == 1) ? "charm" : "charms")} to buy this item."
                            ));
                            break;
                        case "RANCIDEGGS":
                            costs.Add(new ItemChanger.Modules.CumulativeRancidEggCost(entry.Value));
                            break;
                        default:
                            Archipelago.Instance.LogError($"Encountered UNKNOWN currency type {entry.Key} at location {originalLocation}!");
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
                } else
                {
                    costTag.Cost = new MultiCost(costs);
                }
                if(pmt is ISingleCostPlacement scp)
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
