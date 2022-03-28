using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Archipelago.HollowKnight.SlotData
{
    internal static class SlotDataExtract
    {
        public static T ExtractObjectFromSlotData<T>(object v) where T : class
        {
            var jobj = v as JObject;
            var costsDict = jobj?.ToObject<T>() ?? default;
            Archipelago.Instance.LogDebug($"Successfully read object from SlotData: {jobj}");
            return costsDict;
        }
    }
}
