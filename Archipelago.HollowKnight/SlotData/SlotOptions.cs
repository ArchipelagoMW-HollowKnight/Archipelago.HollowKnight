using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Archipelago.HollowKnight;

namespace Archipelago.HollowKnight.SlotData
{
    public class SlotOptions
    {
        [JsonProperty("RandomizeDreamers")]
        public bool RandomizeDreamers { get; set; }

        [JsonProperty("RandomizeSkills")]
        public bool RandomizeSkills { get; set; }

        [JsonProperty("RandomizeFocus")]
        public bool RandomizeFocus { get; set; }

        [JsonProperty("RandomizeSwim")]
        public bool RandomizeSwim { get; set; }

        [JsonProperty("RandomizeCharms")]
        public bool RandomizeCharms { get; set; }

        [JsonProperty("RandomizeKeys")]
        public bool RandomizeKeys { get; set; }

        [JsonProperty("RandomizeMaskShards")]
        public bool RandomizeMaskShards { get; set; }

        [JsonProperty("RandomizeVesselFragments")]
        public bool RandomizeVesselFragments { get; set; }

        [JsonProperty("RandomizeCharmNotches")]
        public bool RandomizeCharmNotches { get; set; }

        [JsonProperty("RandomizePaleOre")]
        public bool RandomizePaleOre { get; set; }

        [JsonProperty("RandomizeGeoChests")]
        public bool RandomizeGeoChests { get; set; }

        [JsonProperty("RandomizeJunkPitChests")]
        public bool RandomizeJunkPitChests { get; set; }

        [JsonProperty("RandomizeRancidEggs")]
        public bool RandomizeRancidEggs { get; set; }

        [JsonProperty("RandomizeRelics")]
        public bool RandomizeRelics { get; set; }

        [JsonProperty("RandomizeWhisperingRoots")]
        public bool RandomizeWhisperingRoots { get; set; }

        [JsonProperty("RandomizeBossEssence")]
        public bool RandomizeBossEssence { get; set; }

        [JsonProperty("RandomizeGrubs")]
        public bool RandomizeGrubs { get; set; }

        [JsonProperty("RandomizeMimics")]
        public bool RandomizeMimics { get; set; }

        [JsonProperty("RandomizeMaps")]
        public bool RandomizeMaps { get; set; }

        [JsonProperty("RandomizeStags")]
        public bool RandomizeStags { get; set; }

        [JsonProperty("RandomizeLifebloodCocoons")]
        public bool RandomizeLifebloodCocoons { get; set; }

        [JsonProperty("RandomizeGrimmkinFlames")]
        public bool RandomizeGrimmkinFlames { get; set; }

        [JsonProperty("RandomizeJournalEntries")]
        public bool RandomizeJournalEntries { get; set; }

        [JsonProperty("RandomizeNail")]
        public bool RandomizeNail { get; set; }

        [JsonProperty("RandomizeGeoRocks")]
        public bool RandomizeGeoRocks { get; set; }

        [JsonProperty("RandomizeBossGeo")]
        public bool RandomizeBossGeo { get; set; }

        [JsonProperty("RandomizeSoulTotems")]
        public bool RandomizeSoulTotems { get; set; }

        [JsonProperty("RandomizeLoreTablets")]
        public bool RandomizeLoreTablets { get; set; }

        [JsonProperty("PreciseMovement")]
        public bool PreciseMovement { get; set; }

        [JsonProperty("ProficientCombat")]
        public bool ProficientCombat { get; set; }

        [JsonProperty("BackgroundObjectPogos")]
        public bool BackgroundObjectPogos { get; set; }

        [JsonProperty("EnemyPogos")]
        public bool EnemyPogos { get; set; }

        [JsonProperty("ObscureSkips")]
        public bool ObscureSkips { get; set; }

        [JsonProperty("ShadeSkips")]
        public bool ShadeSkips { get; set; }

        [JsonProperty("InfectionSkips")]
        public bool InfectionSkips { get; set; }

        [JsonProperty("FireballSkips")]
        public bool FireballSkips { get; set; }

        [JsonProperty("SpikeTunnels")]
        public bool SpikeTunnels { get; set; }

        [JsonProperty("AcidSkips")]
        public bool AcidSkips { get; set; }

        [JsonProperty("DamageBoosts")]
        public bool DamageBoosts { get; set; }

        [JsonProperty("DangerousSkips")]
        public bool DangerousSkips { get; set; }

        [JsonProperty("DarkRooms")]
        public bool DarkRooms { get; set; }

        [JsonProperty("ComplexSkips")]
        public bool ComplexSkips { get; set; }

        [JsonProperty("DifficultSkips")]
        public bool DifficultSkips { get; set; }

        [JsonProperty("RemoveSpellUpgrades")]
        public bool RemoveSpellUpgrades { get; set; }

        [JsonProperty("RandomizeElevatorPass")]
        public bool RandomizeElevatorPass { get; set; }

        [JsonProperty("StartLocation")]
        public int StartLocation { get; set; }

        [JsonProperty("MinimumGrubPrice")]
        public int MinGrubPrice { get; set; }

        [JsonProperty("MaximumGrubPrice")]
        public int MaxGrubPrice { get; set; }

        [JsonProperty("MinimumEssencePrice")]
        public int MinEssencePrice { get; set; }

        [JsonProperty("MaximumEssencePrice")]
        public int MaxEssencePrice { get; set; }

        [JsonProperty("MinimumEggPrice")]
        public int MinEggPrice { get; set; }

        [JsonProperty("MaximumEggPrice")]
        public int MaxEggPrice { get; set; }

        [JsonProperty("RandomCharmCosts")]
        public int RandomCharmCosts { get; set; }

        [JsonProperty("EggShopSlots")]
        public int EggShopSlots { get; set; }

        // Even though this is encoded as an int, it doesn't import properly without doing this.
        [JsonConverter(typeof(StringEnumConverter))]
        public GoalsLookup Goal { get; set; }

        // Even though this is encoded as an int, it doesn't import properly without doing this.
        [JsonConverter(typeof(StringEnumConverter))]
        public DeathLinkType DeathLink { get; set; }

        [JsonProperty("StartingGeo")]
        public int StartingGeo { get; set; }
    }
}
