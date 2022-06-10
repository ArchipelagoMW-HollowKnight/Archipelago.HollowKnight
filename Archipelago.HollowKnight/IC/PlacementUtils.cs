using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ItemChanger;
using Archipelago.HollowKnight.IC;
using Archipelago.MultiClient.Net.Packets;

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

        internal static void CreateLocationHint(AbstractPlacement pmt)
        {
            if(pmt == null)
            {
                Archipelago.Instance.LogError("CreateLocationHint called for a NULL Placement!");
                return;
            }
            // Trying to find out why this is dying.
            ArchipelagoItemTag tag;
            List<ArchipelagoItemTag> hintedTags = new();
            List<long> hintedLocations = new();

            // Find the location IDs associated with all of our placed items 
            foreach (AbstractItem item in pmt.Items)
            {
                if (item.GetTag<ArchipelagoItemTag>(out tag) && !tag.Hinted)
                { 
                    tag.Hinted = true;
                    hintedTags.Add(tag);
                    hintedLocations.Add(tag.Location);
                }
            }
            // Hint them if we found any.
            if (hintedLocations.Any())
            {
                Archipelago.Instance.LogError($"Hinting {hintedLocations.Count()} locations.");

                // Mark as hinted immediately, but later set the actual hinted status to match the sendPacketAsync result so it doesn't stay hinted if the connection is down.
                Archipelago.Instance.session.Socket.SendPacketAsync(new LocationScoutsPacket()
                {
                    CreateAsHint = true,
                    Locations = hintedLocations.ToArray(),
                }, (result) => { foreach (ArchipelagoItemTag tag in hintedTags) { tag.Hinted = result; } });
            }
        }
    }
}
