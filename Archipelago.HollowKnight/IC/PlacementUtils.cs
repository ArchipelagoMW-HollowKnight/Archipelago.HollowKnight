using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ItemChanger;
using Archipelago.HollowKnight.IC;
using Archipelago.MultiClient.Net.Packets;
using Archipelago.MultiClient.Net.Exceptions;

namespace Archipelago.HollowKnight
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
