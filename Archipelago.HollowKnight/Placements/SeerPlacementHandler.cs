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
    internal class SeerPlacementHandler : IPlacementHandler
    {
        public Dictionary<string, int> SeerCosts { get; }

        public SeerPlacementHandler(Dictionary<string, int> seerCosts)
        {
            SeerCosts = seerCosts;
        }

        public bool CanHandlePlacement(string location)
        {
            return location.StartsWith("Seer");
        }

        public void HandlePlacement(AbstractPlacement pmt, AbstractItem item, string originalLocationName)
        {
            var costChestPlacement = pmt as CostChestPlacement;
            if (!costChestPlacement.HasTag<DestroySeerRewardTag>())
            {
                var tag = costChestPlacement.AddTag<DestroySeerRewardTag>();
                tag.destroyRewards = SeerRewards.All & ~SeerRewards.GladeDoor & ~SeerRewards.Ascension;
            }
            costChestPlacement.AddItem(item, Cost.NewEssenceCost(GetEssenceCostForLocation(originalLocationName)));
        }

        private int GetEssenceCostForLocation(string originalLocation)
        {
            if (SeerCosts.TryGetValue(originalLocation, out var cost))
            {
                return cost;
            }
            else
            {
                Archipelago.Instance.LogError($"Attempted to get Essence cost for location '{originalLocation}' but key was not present.");
                return 0;
            }
        }
    }
}
