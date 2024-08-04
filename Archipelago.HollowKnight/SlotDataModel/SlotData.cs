using Newtonsoft.Json;
using System.Collections.Generic;

namespace Archipelago.HollowKnight.SlotDataModel
{
    public class SlotData
    {
        [JsonProperty("seed")]
        public int Seed { get; set; }

        [JsonProperty("options")]
        public SlotOptions Options { get; set; }

        [JsonProperty("location_costs")]
        public Dictionary<string, Dictionary<string, int>> LocationCosts { get; set; }

        [JsonProperty("notch_costs")]
        public List<int> NotchCosts { get; set; }

        [JsonProperty("grub_count")]
        public int? GrubsRequired { get; set; }
    }
}
