using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Archipelago.HollowKnight.SlotData
{
    internal static class SlotDataExtract
    {
        public static T ExtractObjectFromSlotData<T>(object v) where T : class
        {
            JObject jobj = v as JObject;
            T extractedObject = jobj?.ToObject<T>() ?? default;
            Archipelago.Instance.LogDebug($"Successfully read object from SlotData: {jobj}");
            return extractedObject;
        }
        public static T ExtractArrayFromSlotData<T>(object v) where T : class
        {
            JArray jarr = v as JArray;
            T extractedObject = jarr?.ToObject<T>() ?? default;
            Archipelago.Instance.LogDebug($"Successfully read object from SlotData: {jarr}");
            return extractedObject;
        }

        public static Dictionary<string, Dictionary<string, int>> ExtractLocationCostsFromSlotData(object v)
        {
            Dictionary<string, Dictionary<string, int>> ret = new();
            Dictionary<string, JObject> jobj = ExtractObjectFromSlotData<Dictionary<string, JObject>>(v);
            foreach (string key in jobj.Keys)
            {
                ret[key] = jobj[key].ToObject<Dictionary<string, int>>();
            }
            return ret;
        }
    }
}
