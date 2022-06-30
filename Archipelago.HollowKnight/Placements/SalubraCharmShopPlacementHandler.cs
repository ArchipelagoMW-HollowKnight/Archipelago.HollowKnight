using System;
using System.Collections.Generic;
using ItemChanger;
using ItemChanger.Placements;

namespace Archipelago.HollowKnight.Placements
{
    internal class SalubraCharmShopPlacementHandler : ShopPlacementHandler
    {
        public Dictionary<string, int> CharmCosts { get; }

        public SalubraCharmShopPlacementHandler(Dictionary<string, int> charmCosts, Random Random) : base(Random)
        {
            CharmCosts = charmCosts;
        }

        public override bool CanHandlePlacement(string location)
        {
            return location.StartsWith("Salubra_(Requires_Charms)");
        }

        public override void HandlePlacement(AbstractPlacement pmt, AbstractItem item, string originalLocationName)
        {
            var shopPlacement = pmt as ShopPlacement;
            var charmCostAmount = GetCharmCostForLocation(originalLocationName);
            var charmCost = new PDIntCost(charmCostAmount, nameof(PlayerData.charmsOwned), $"Acquire {charmCostAmount} total charms to buy this item.");
            var cost = new MultiCost(Cost.NewGeoCost(GetGeoCost(item)), charmCost);

            shopPlacement.AddItemWithCost(item, cost);
        }

        private int GetCharmCostForLocation(string originalLocation)
        {
            if (CharmCosts.TryGetValue(originalLocation, out var cost))
            {
                return cost;
            }
            else
            {
                Archipelago.Instance.LogError($"Attempted to get Charm cost for location '{originalLocation}' but key was not present.");
                return 0;
            }
        }
    }
}
