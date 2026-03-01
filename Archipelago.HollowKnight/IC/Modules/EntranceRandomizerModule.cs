using ItemChanger;
using ItemChanger.Modules;
using System;
using System.Collections.Generic;

namespace Archipelago.HollowKnight.IC.Modules
{
    public class EntranceRandomizerModule : ItemChanger.Modules.Module
    {
        public override void Initialize()
        {
            Dictionary<string, string> entrances = ArchipelagoMod.Instance.SlotData.EntrancePairs;

            if (entrances == null || entrances.Count == 0)
            {
                ArchipelagoMod.Instance.Log("[ER] No entrance pairings provided");
                return;
            }

            ArchipelagoMod.Instance.Log($"[ER] EntranceRandomizerModule initializing with {entrances.Count} entrances");

            foreach (var pair in entrances)
            {
                if (pair.Key == null || pair.Value == null)
                {
                    continue;
                }

                try
                {
                    var sourceTransition = ParseTransition(pair.Key);
                    var targetTransition = ParseTransition(pair.Value);

                    ItemChangerMod.AddTransitionOverride(sourceTransition, targetTransition);

                    ArchipelagoMod.Instance.Log(
                        $"[ER] Overrode transition: {pair.Key} -> {pair.Value}"
                    );
                }
                catch (Exception ex)
                {
                    ArchipelagoMod.Instance.LogWarn(
                        $"[ER] Failed to parse transition pair {pair.Key} -> {pair.Value}: {ex.Message}"
                    );
                }
            }

            ArchipelagoMod.Instance.Log($"[ER] Finished overriding transitions");
        }

        public override void Unload()
        {

        }

        private Transition ParseTransition(string transitionString)
        {
            int bracketStart = transitionString.IndexOf('[');
            int bracketEnd = transitionString.IndexOf(']');

            if (bracketStart < 0 || bracketEnd < 0)
            {
                throw new ArgumentException($"Invalid transition format: {transitionString}");
            }

            string sceneName = transitionString[..bracketStart].Trim();
            string doorName = transitionString[(bracketStart + 1)..bracketEnd].Trim();

            return new Transition(sceneName, doorName);
        }
    }
}
