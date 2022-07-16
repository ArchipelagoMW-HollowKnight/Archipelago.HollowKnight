using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Archipelago.HollowKnight.SlotData
{
    internal static class SlotDataExtract
    {
        public static T ExtractObjectFromSlotData<T>(object v) where T : class
        {
            var jobj = v as JObject;
            var extractedObject = jobj?.ToObject<T>() ?? default;
            Archipelago.Instance.LogDebug($"Successfully read object from SlotData: {jobj}");
            return extractedObject;
        }
        public static T ExtractArrayFromSlotData<T>(object v) where T : class
        {
            var jarr = v as JArray;
            var extractedObject = jarr?.ToObject<T>() ?? default;
            Archipelago.Instance.LogDebug($"Successfully read object from SlotData: {jarr}");
            return extractedObject;
        }

        public static Dictionary<string, Dictionary<string, int>> ExtractLocationCostsFromSlotData(object v)
        {
            Dictionary<string, Dictionary<string, int>> ret = new();
            var jobj = ExtractObjectFromSlotData<Dictionary<string, JObject>>(v);
            foreach (var key in jobj.Keys)
            {
                ret[key] = jobj[key].ToObject<Dictionary<string, int>>();
            }
            return ret;
        }
    }
}
