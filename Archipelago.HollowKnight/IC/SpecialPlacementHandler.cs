using System;
using System.Collections.Generic;
using System.Linq;
using ItemChanger;
using ItemChanger.Placements;
using ItemChanger.Tags;

namespace Archipelago.HollowKnight.IC
{
    internal static class SpecialPlacementHandler
    {
        public static Dictionary<string, int> GrubFatherCosts;
        public static Dictionary<string, int> SeerCosts;
        public static Dictionary<string, int> EggCosts;
        public static Random Random;

        public static bool IsEggShopPlacement(string location)
        {
            return location.StartsWith("Egg_Shop");
        }

        public static bool IsSeerPlacement(string location)
        {
            return location.StartsWith("Seer");
        }

        public static bool IsGrubfatherPlacement(string location)
        {
            return location.StartsWith("Grubfather");
        }

        public static bool IsShopPlacement(string location)
        {
            var names = new[] { LocationNames.Sly, LocationNames.Sly_Key, LocationNames.Iselda, LocationNames.Salubra, LocationNames.Leg_Eater };
            return names.Any(x => location.StartsWith(x));
        }

        public static void PlaceGrubfatherItem(string originalLocation, AbstractPlacement pmt, AbstractItem item)
        {
            var costChestPlacement = pmt as CostChestPlacement;

            item.OnGive += (x) => Archipelago.Instance.LogDebug($"Running OnGive for grubfather placement in location {originalLocation}.");

            if (!costChestPlacement.HasTag<DestroyGrubRewardTag>())
            {
                var tag = costChestPlacement.AddTag<DestroyGrubRewardTag>();
                tag.destroyRewards = GrubfatherRewards.AllNonGeo;
            }

            costChestPlacement.AddItem(item, Cost.NewGrubCost(GetGrubCostForLocation(originalLocation)));
        }

        private static int GetGrubCostForLocation(string originalLocation)
        {
            if (GrubFatherCosts.TryGetValue(originalLocation, out var cost))
            {
                return cost;
            }
            else
            {
                Archipelago.Instance.LogError($"Attempted to get Grub cost for location '{originalLocation}' but key was not present.");
                return 0;
            }
        }

        public static void PlaceEggShopItem(AbstractPlacement pmt, AbstractItem item)
        {
            // TODO: Note: When rancid eggs are randomized, Tuk does not sell eggs. (in rando4 at least)
            var eggShopPlacement = pmt as EggShopPlacement;
            var tag = item.AddTag<CostTag>();
            tag.Cost = new ItemChanger.Modules.CumulativeRancidEggCost(1);
            eggShopPlacement.Add(item);
        }

        public static void PlaceSeerItem(string originalLocation, AbstractPlacement pmt, AbstractItem item)
        {
            var costChestPlacement = pmt as CostChestPlacement;
            if (!costChestPlacement.HasTag<DestroySeerRewardTag>())
            {
                var tag = costChestPlacement.AddTag<DestroySeerRewardTag>();
                tag.destroyRewards = SeerRewards.All & ~SeerRewards.GladeDoor & ~SeerRewards.Ascension;
            }
            costChestPlacement.AddItem(item, Cost.NewEssenceCost(GetEssenceCostForLocation(originalLocation)));
        }

        private static int GetEssenceCostForLocation(string originalLocation)
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

        public static void PlaceShopItem(AbstractPlacement pmt, AbstractItem item)
        {
            var shopPlacement = pmt as ShopPlacement;
            shopPlacement.AddItemWithCost(item, GenerateGeoCost());
        }

        private static Cost GenerateGeoCost()
        {
            return Cost.NewGeoCost(Random.Next(1, 400));
        }
    }
}
