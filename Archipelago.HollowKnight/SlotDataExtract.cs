using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Archipelago.HollowKnight
{
    internal static class SlotDataExtract
    {
        public static Dictionary<string, int> ExtractCostsFromSlotData(object v)
        {
            var jobj = v as JObject;
            var costsDict = jobj?.ToObject<Dictionary<string, int>>();
            Archipelago.Instance.LogDebug($"Successfully read costs from SlotData: {jobj}");
            return costsDict;
        }
    }
}
