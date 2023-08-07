using Archipelago.MultiClient.Net.Models;
using System.Collections.Generic;
using System.Linq;

namespace Archipelago.HollowKnight
{
    public class GroupUtil
    {
        public static bool WillItemRouteToMe(int destinationSlot)
        {
            int mySlot = Archipelago.Instance.Slot;
            IReadOnlyDictionary<int, NetworkSlot> allSlots = Archipelago.Instance.AllSlots;
            if (destinationSlot == mySlot)
            {
                return true;
            }
            else if (allSlots.TryGetValue(destinationSlot, out NetworkSlot destination))
            {
                return destination.GroupMembers.Contains(mySlot);
            }
            else
            {
                return false;
            }
        }
    }
}
