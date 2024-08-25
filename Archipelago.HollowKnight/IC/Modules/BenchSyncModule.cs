using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.Models;
using Benchwarp;
using ItemChanger.Modules;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Archipelago.HollowKnight.IC.Modules
{
    public partial class BenchSyncModule : Module
    {
        private const string DATASTORAGE_KEY_UNLOCKED_BENCHES = "unlocked_benches";
        private const string BENCH_KEY_SEPARATOR = ":::";

        private ArchipelagoSession session;

        private Dictionary<BenchKey, Bench> benchLookup;

        [DataStorageProperty(nameof(session), Scope.Slot, DATASTORAGE_KEY_UNLOCKED_BENCHES)]
        private readonly DataStorageElement _unlockedBenches;

        public override async void Initialize()
        {
            session = ArchipelagoMod.Instance.session;
            benchLookup = Bench.Benches.ToDictionary(x => x.ToBenchKey(), x => x);

            Benchwarp.Events.OnBenchUnlock += OnUnlockLocalBench;
            UnlockedBenches.Initialize(JObject.FromObject(new Dictionary<string, bool>()));

            UnlockedBenches.OnValueChanged += OnUnlockRemoteBench;
            Dictionary<string, bool> benchData = BuildBenchData(Bench.Benches.Where(x => x.HasVisited()).Select(x => x.ToBenchKey()));
            UnlockedBenches += Operation.Update(benchData);

            try
            {
                Dictionary<string, bool> benches = await UnlockedBenches.GetAsync<Dictionary<string, bool>>();
                UnlockBenches(benches);
            } 
            catch (Exception ex)
            {
                ArchipelagoMod.Instance.LogError($"Unexpected issue unlocking benches from server data: {ex}");
            }
        }

        public override void Unload()
        {
            Benchwarp.Events.OnBenchUnlock -= OnUnlockLocalBench;
            UnlockedBenches.OnValueChanged -= OnUnlockRemoteBench;
        }

        private void OnUnlockLocalBench(BenchKey obj)
        {
            UnlockedBenches += Operation.Update(BuildBenchData([obj]));
        }

        private void OnUnlockRemoteBench(JToken oldData, JToken newData, Dictionary<string, JToken> args)
        {
            Dictionary<string, bool> benches = newData.ToObject<Dictionary<string, bool>>();
            UnlockBenches(benches);
        }

        private Dictionary<string, bool> BuildBenchData(IEnumerable<BenchKey> keys)
        {
            Dictionary<string, bool> obtainedBenches = new();
            foreach (BenchKey key in keys)
            {
                obtainedBenches[$"{key.SceneName}{BENCH_KEY_SEPARATOR}{key.RespawnMarkerName}"] = true;
            }
            return obtainedBenches;
        }

        private void UnlockBenches(Dictionary<string, bool> benches)
        {
            if (benches == null)
            {
                return;
            }

            foreach (KeyValuePair<string, bool> kv in benches)
            {
                string[] keyParts = kv.Key.Split([BENCH_KEY_SEPARATOR], StringSplitOptions.None);
                BenchKey key = new(keyParts[0], keyParts[1]);
                if (benchLookup.TryGetValue(key, out Bench bench))
                {
                    bench.SetVisited(kv.Value);
                }
            }
        }
    }
}
