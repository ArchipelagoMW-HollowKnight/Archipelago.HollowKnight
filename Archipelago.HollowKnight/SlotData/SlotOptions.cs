using Newtonsoft.Json;

namespace Archipelago.HollowKnight.SlotData
{
    // TODO: change to match new slot options someday
    internal class SlotOptions
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

        [JsonProperty("RandomizeElevatorPass")]
        public bool RandomizeElevatorPass { get; set; }

        [JsonProperty("RandomizeSplitCloak")]
        public bool RandomizeSplitCloak { get; set; }

        [JsonProperty("RandomizeSplitClaw")]
        public bool RandomizeSplitClaw { get; set; }

        [JsonProperty("RandomizeNail")]
        public bool RandomizeNail { get; set; }

        [JsonProperty("RandomizeSplitSuperdash")]
        public bool RandomizeSplitSuperdash { get; set; }

        [JsonProperty("RandomizeEggShop")]
        public bool RandomizeEggShop { get; set; }

        [JsonProperty("RandomizeGeoRocks")]
        public bool RandomizeGeoRocks { get; set; }

        [JsonProperty("RandomizeBossGeo")]
        public bool RandomizeBossGeo { get; set; }

        [JsonProperty("RandomizeSoulTotems")]
        public bool RandomizeSoulTotems { get; set; }

        [JsonProperty("RandomizeLoreTablets")]
        public bool RandomizeLoreTablets { get; set; }

        [JsonProperty("acidskips")]
        public bool AcidSkips { get; set; }

        [JsonProperty("backgroundpogos")]
        public bool BackgroundPogos { get; set; }

        [JsonProperty("enemypogos")]
        public bool EnemyPogos { get; set; }

        [JsonProperty("obscureskips")]
        public bool ObscureSkips { get; set; }

        [JsonProperty("randomelevators")]
        public bool RandomElevators { get; set; }

        [JsonProperty("precisemovement")]
        public bool PreciseMovement { get; set; }

        [JsonProperty("randomfocus")]
        public bool RandomFocus { get; set; }

        [JsonProperty("shadeskips")]
        public bool ShadeSkips { get; set; }

        [JsonProperty("dangerousskips")]
        public bool DangerousSkips { get; set; }

        [JsonProperty("darkrooms")]
        public bool DarkRooms { get; set; }

        [JsonProperty("randomnail")]
        public bool RandomNail { get; set; }

        [JsonProperty("infectionskips")]
        public bool InfectionSkips { get; set; }

        [JsonProperty("proficientcombat")]
        public bool ProficientCombat { get; set; }

        [JsonProperty("cursed")]
        public bool Cursed { get; set; }

        [JsonProperty("complexskips")]
        public bool ComplexSkips { get; set; }

        [JsonProperty("damageboosts")]
        public bool DamageBoosts { get; set; }

        [JsonProperty("spiketunnels")]
        public bool SpikeTunnels { get; set; }

        [JsonProperty("difficultskips")]
        public bool DifficultSkips { get; set; }

        [JsonProperty("fireballskips")]
        public bool FireballSkips { get; set; }

        [JsonProperty("start_location")]
        public int StartLocation { get; set; }

        [JsonProperty("minimum_grub_price")]
        public int MinimumGrubPrice { get; set; }

        [JsonProperty("maximum_grub_price")]
        public int MaximumGrubPrice { get; set; }

        [JsonProperty("minimum_essence_price")]
        public int MinimumEssencePrice { get; set; }

        [JsonProperty("maximum_essence_price")]
        public int MaximumEssencePrice { get; set; }

        [JsonProperty("minimum_egg_price")]
        public int MinimumEggPrice { get; set; }

        [JsonProperty("maximum_egg_price")]
        public int MaximumEggPrice { get; set; }

        [JsonProperty("random_charm_costs")]
        public int RandomCharmCosts { get; set; }
    }
}
