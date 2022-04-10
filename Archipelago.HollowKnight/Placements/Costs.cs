using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ItemChanger;

namespace Archipelago.HollowKnight.Placements
{
    internal static class Costs
    {
        public static Random Random { get; set; }
        public static Cost GenerateGeoCost()
        {
            return Cost.NewGeoCost(Random.Next(1, 400));
        }
    }
}
