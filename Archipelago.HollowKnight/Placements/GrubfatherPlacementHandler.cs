using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ItemChanger;
using ItemChanger.Placements;
using ItemChanger.Tags;

namespace Archipelago.HollowKnight.Placements
{
    internal class GrubfatherPlacementHandler : IPlacementHandler
    {
        public Dictionary<string, int> GrubCosts { get; }

        public GrubfatherPlacementHandler(Dictionary<string, int> grubCosts)
        {
            GrubCosts = grubCosts;
        }

        public bool CanHandlePlacement(string location)
        {
            return location.StartsWith("Grubfather");
        }

        public void HandlePlacement(AbstractPlacement pmt, AbstractItem item, string originalLocationName)
        {
            var costChestPlacement = pmt as CostChestPlacement;
            if (!costChestPlacement.HasTag<DestroyGrubRewardTag>())
            {
                var tag = costChestPlacement.AddTag<DestroyGrubRewardTag>();
                tag.destroyRewards = GrubfatherRewards.AllNonGeo;
            }
            costChestPlacement.AddItem(item, Cost.NewGrubCost(GetGrubCostForLocation(originalLocationName)));
        }

        private int GetGrubCostForLocation(string originalLocation)
        {
            if (GrubCosts.TryGetValue(originalLocation, out var cost))
            {
                return cost;
            }
            else
            {
                Archipelago.Instance.LogError($"Attempted to get Grub cost for location '{originalLocation}' but key was not present.");
                return 0;
            }
        }
    }
}
