using Archipelago.HollowKnight.IC;
using Archipelago.HollowKnight.Placements;
using Archipelago.HollowKnight.SlotData;
using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Models;
using Archipelago.MultiClient.Net.Packets;
using ItemChanger;
using ItemChanger.Extensions;
using ItemChanger.Items;
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
            GrubfatherCosts = SlotDataExtract.ExtractObjectFromSlotData<Dictionary<string, int>>(slotData["Grub_costs"]);
            SeerCosts = SlotDataExtract.ExtractObjectFromSlotData<Dictionary<string, int>>(slotData["Essence_costs"]);
            EggCosts = SlotDataExtract.ExtractObjectFromSlotData<Dictionary<string, int>>(slotData["Egg_costs"]);
            SalubraCharmCosts = SlotDataExtract.ExtractObjectFromSlotData<Dictionary<string, int>>(slotData["Charm_costs"]);
            NotchCosts = SlotDataExtract.ExtractArrayFromSlotData<List<int>>(slotData["notch_costs"]);

            placementHandlers = new List<IPlacementHandler>()
            {
                new ShopPlacementHandler(Random),
                new GrubfatherPlacementHandler(GrubfatherCosts),
                new SeerPlacementHandler(SeerCosts),
                new EggShopPlacementHandler(EggCosts),
                new SalubraCharmShopPlacementHandler(SalubraCharmCosts, Random)
            };
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
                    foreach (var item in packet.Locations)
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

            //ItemChangerMod.AddPlacements(pmt.Yield());
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
