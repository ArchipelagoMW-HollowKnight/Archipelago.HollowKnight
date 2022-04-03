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
    }
}
