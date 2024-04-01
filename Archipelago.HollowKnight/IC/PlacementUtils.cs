using ItemChanger;
using System.Collections.Generic;

namespace Archipelago.HollowKnight.IC
{
    internal static class PlacementUtils
    {
        internal static IEnumerable<long> GetLocationIDs(AbstractPlacement pmt)
        {
            ArchipelagoItemTag tag;
            foreach (AbstractItem item in pmt.Items)
            {
                tag = item.GetTag<ArchipelagoItemTag>();
                if (tag != null)
                {
                    yield return tag.Location;
                }
            }
        }
    }
}
