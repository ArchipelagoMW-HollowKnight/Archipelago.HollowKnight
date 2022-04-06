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
            var names = new[] { LocationNames.Sly, LocationNames.Sly_Key, LocationNames.Iselda, LocationNames.Leg_Eater };
            return (names.Any(x => location.StartsWith(x)) || location == LocationNames.Salubra) && !location.Contains("Requires_Charms");
        }

        public void HandlePlacement(AbstractPlacement pmt, AbstractItem item, string originalLocationName)
        {
            var shopPlacement = pmt as ShopPlacement;
            shopPlacement.AddItemWithCost(item, Costs.GenerateGeoCost());
        }
    }
}
