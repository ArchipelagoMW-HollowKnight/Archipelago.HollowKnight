using System.Collections.Generic;
using ItemChanger;
using ItemChanger.Items;

namespace Archipelago.HollowKnight.Grants
{
    internal static class GeoRockGrants
    {
        public static bool IsGeoRockItem(string itemName)
        {
            return itemName.StartsWith("Geo_Rock");
        }

        /// <summary>
        /// Ran into an issue where receiving Geo Rock items from AP caused crashing. This is my alternative.
        /// If you know why this might happen or have a better way, please help.
        /// </summary>
        public static void RewardGeoFromItemsSafely(List<AbstractItem> items)
        {
            var amount = 0;
            foreach (AbstractItem item in items)
            {
                var geoRockItem = item as GeoRockItem;
                if (geoRockItem != null)
                {
                    amount += geoRockItem.amount;
                }
            }

            PlayerData.instance.AddGeo(amount);
        }
    }
}
