using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ItemChanger;
using ItemChanger.Placements;
using ItemChanger.Items;

namespace Archipelago.HollowKnight.Placements
{
    internal class ShopPlacementHandler : IPlacementHandler
    {
        private readonly Random Random;

        public ShopPlacementHandler(Random random)
        {
            Random = random;
        }
        public virtual bool CanHandlePlacement(string location)
        {
            var names = new[] { LocationNames.Sly, LocationNames.Sly_Key, LocationNames.Iselda, LocationNames.Leg_Eater, LocationNames.Salubra };
            return names.Any(x => location.StartsWith(x)) && !location.Contains("Requires_Charms");
        }

        protected int GetGeoCost(AbstractItem item)
        {
            if (item is AddGeoItem || item is LoreItem || item is SpawnGeoItem || item is GeoRockItem)
            {
                return 1;
            }
            else
            {
                return Random.Next(1, 400);
            }

        }

        public virtual void HandlePlacement(AbstractPlacement pmt, AbstractItem item, string originalLocationName)
        {
            var shopPlacement = pmt as ShopPlacement;
            shopPlacement.AddItemWithCost(item, Cost.NewGeoCost(GetGeoCost(item)));
            shopPlacement.defaultShopItems = DefaultShopItems.IseldaMapPins | DefaultShopItems.IseldaMapMarkers | DefaultShopItems.LegEaterRepair;
        }
    }
}
