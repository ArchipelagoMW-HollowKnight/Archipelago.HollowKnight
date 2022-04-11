using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ItemChanger;
using ItemChanger.Placements;
namespace Archipelago.HollowKnight.Placements
{
    internal class ShopPlacementHandler : IPlacementHandler
    {
        public bool CanHandlePlacement(string location)
        {
            var names = new[] { LocationNames.Sly, LocationNames.Sly_Key, LocationNames.Iselda, LocationNames.Leg_Eater, LocationNames.Salubra };
            return names.Any(x => location.StartsWith(x)) && !location.Contains("Requires_Charms");
        }

        public void HandlePlacement(AbstractPlacement pmt, AbstractItem item, string originalLocationName)
        {
            var shopPlacement = pmt as ShopPlacement;
            shopPlacement.AddItemWithCost(item, Costs.GenerateGeoCost());
            shopPlacement.defaultShopItems = this.GetDefaultShopItems();
        }

        public DefaultShopItems GetDefaultShopItems()
        {
            var options = Archipelago.Instance.SlotOptions;
            DefaultShopItems items = DefaultShopItems.IseldaMapPins | DefaultShopItems.IseldaMapMarkers | DefaultShopItems.LegEaterRepair;

            // Uncomment these if/when we stop sending vanilla placements from AP
            /*
            if(!options.RandomizeKeys)
            {
                items |= DefaultShopItems.SlyLantern;
                items |= DefaultShopItems.SlySimpleKey;
                items |= DefaultShopItems.SlyKeyElegantKey;
            }

            if (!options.RandomizeCharms)
            {
                items |= DefaultShopItems.SlyCharms;
                items |= DefaultShopItems.SlyKeyCharms;
                items |= DefaultShopItems.IseldaCharms;
                items |= DefaultShopItems.SalubraCharms;
                items |= DefaultShopItems.LegEaterCharms;
            }

            if (!options.RandomizeMaps)
            {
                items |= DefaultShopItems.IseldaQuill;
                items |= DefaultShopItems.IseldaMaps;
            }

            if (!options.RandomizeMaskShards)
            {
                items |= DefaultShopItems.SlyMaskShards;
            }

            if (!options.RandomizeVesselFragments)
            {
                items |= DefaultShopItems.SlyVesselFragments;
            }

            if (!options.RandomizeRancidEggs)
            {
                items |= DefaultShopItems.SlyRancidEgg;
            }

            // FIXME: Salubra charm notches.
            // Ref: https://github.com/homothetyhk/RandomizerMod/blob/39c6feeb7a4c8c94eef62481dec8864027110ec4/RandomizerMod/IC/Shops.cs#L58
            */
            return items;
        }
    }
}
