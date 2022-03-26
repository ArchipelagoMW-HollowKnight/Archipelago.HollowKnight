using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ItemChanger;

namespace Archipelago.HollowKnight
{
    internal class VanillaShops
    {
        private AbstractPlacement[] Sly = new AbstractPlacement[14];
        private AbstractPlacement[] Iselda = new AbstractPlacement[27];

        public static bool IsLocationAShop(string locationName)
        {
            return new string[] {"Iselda", "Leg_Eater", "Sly", "Salubra", "Egg_Shop"}.Any(x => locationName.StartsWith(x));
        }

        public static bool IsLocationAProgressiveShop(string locationName)
        {
            return new string[] { "Seer", "Grubfather" }.Any(x => locationName.StartsWith(x));
        }
    }
}
