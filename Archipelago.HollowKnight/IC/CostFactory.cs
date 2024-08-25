using ItemChanger;
using ItemChanger.Placements;
using ItemChanger.Tags;
using System.Collections.Generic;
using System.Linq;

namespace Archipelago.HollowKnight.IC
{
    internal class CostFactory
    {
        private Dictionary<string, Dictionary<string, int>> locationCosts;

        /// <summary>
        /// Initializes a cost factory with costs provided from slot data
        /// </summary>
        /// <param name="locationCosts">A lookup from placement name -> cost type -> amount</param>
        public CostFactory(Dictionary<string, Dictionary<string, int>> locationCosts)
        {
            this.locationCosts = locationCosts;
        }

        public void ApplyCost(AbstractPlacement pmt, AbstractItem item, string serverLocationName)
        {
            if (locationCosts.TryGetValue(serverLocationName, out Dictionary<string, int> costs))
            {
                List<Cost> icCosts = new();
                foreach (KeyValuePair<string, int> entry in costs)
                {
                    Cost proposedCost = null;
                    switch (entry.Key)
                    {
                        case "GEO":
                            proposedCost = Cost.NewGeoCost(entry.Value);
                            break;
                        case "ESSENCE":
                            proposedCost = Cost.NewEssenceCost(entry.Value);
                            break;
                        case "GRUBS":
                            proposedCost = Cost.NewGrubCost(entry.Value);
                            break;
                        case "CHARMS":
                            proposedCost = new PDIntCost(
                                entry.Value, nameof(PlayerData.charmsOwned),
                                $"Acquire {entry.Value} {((entry.Value == 1) ? "charm" : "charms")}"
                            );
                            break;
                        case "RANCIDEGGS":
                            proposedCost = new ItemChanger.Modules.CumulativeRancidEggCost(entry.Value);
                            break;
                        default:
                            ArchipelagoMod.Instance.LogWarn(
                                $"Encountered UNKNOWN currency type {entry.Key} at location {serverLocationName}!");
                            break;
                    }

                    if (proposedCost != null)
                    {
                        // suppress inherent costs - if the server told us to pay X, but the implementation of
                        // the location will force us to pay Y >= X, we skip adding the cost to prevent doubling up.
                        IEnumerable<Cost> inherentCosts = pmt.GetPlacementAndLocationTags()
                            .OfType<ImplicitCostTag>()
                            .Where(t => t.Inherent)
                            .Select(t => t.Cost);
                        if (inherentCosts.Any(c => c.Includes(proposedCost)))
                        {
                            ArchipelagoMod.Instance.LogDebug($"Supressing cost {entry.Value} {entry.Key} for location {serverLocationName}");
                            continue;
                        }
                        else
                        {
                            icCosts.Add(proposedCost);
                        }
                    }
                }

                if (icCosts.Count == 0)
                {
                    ArchipelagoMod.Instance.LogWarn(
                        $"Found zero cost types when handling placement at location {serverLocationName}!");
                    return;
                }

                Cost finalCosts;
                if (icCosts.Count == 1)
                {
                    finalCosts = icCosts[0];
                }
                else
                {
                    finalCosts = new MultiCost(icCosts);
                }

                if (pmt is ISingleCostPlacement scp)
                {
                    if (scp.Cost == null)
                    {
                        scp.Cost = finalCosts;
                    }
                    else
                    {
                        scp.Cost = new MultiCost(scp.Cost, finalCosts);
                    }
                }
                else
                {
                    CostTag costTag = item.AddTag<CostTag>();
                    costTag.Cost = finalCosts;
                }
            }
        }
    }
}