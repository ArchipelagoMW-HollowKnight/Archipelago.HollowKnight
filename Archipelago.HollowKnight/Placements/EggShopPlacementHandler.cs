using System.Collections.Generic;
using ItemChanger;
using ItemChanger.Placements;

namespace Archipelago.HollowKnight.Placements
{
    internal class EggShopPlacementHandler : IPlacementHandler
    {
        public Dictionary<string, int> EggCosts { get; }

        public EggShopPlacementHandler(Dictionary<string, int> eggCosts)
        {
            EggCosts = eggCosts;
        }

        public bool CanHandlePlacement(string location)
        {
            return location.StartsWith("Egg_Shop");
        }

        public void HandlePlacement(AbstractPlacement pmt, AbstractItem item, string originalLocationName)
        {
            var cost = GetEggCostForLocation(originalLocationName);
            var eggShopPlacement = pmt as EggShopPlacement;
            var tag = item.AddTag<CostTag>();
            tag.Cost = new ItemChanger.Modules.CumulativeRancidEggCost(cost);
            eggShopPlacement.Add(item);
        }

        private int GetEggCostForLocation(string originalLocation)
        {
            if (EggCosts.TryGetValue(originalLocation, out var cost))
            {
                return cost;
            }
            else
            {
                Archipelago.Instance.LogError($"Attempted to get Egg cost for location '{originalLocation}' but key was not present.");
                return 0;
            }
        }
    }
}
