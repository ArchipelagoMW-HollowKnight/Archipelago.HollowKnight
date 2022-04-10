using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ItemChanger;

namespace Archipelago.HollowKnight.Placements
{
    internal interface IPlacementHandler
    {
        bool CanHandlePlacement(string location);
        void HandlePlacement(AbstractPlacement pmt, AbstractItem item, string originalLocationName);
    }
}
