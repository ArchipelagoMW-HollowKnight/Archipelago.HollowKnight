using Archipelago.HollowKnight.IC.Modules;
using Archipelago.MultiClient.Net.Exceptions;
using ItemChanger;
using MonoMod.RuntimeDetour;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Archipelago.HollowKnight
{
    public enum GoalsLookup
    {
        Any = 0,
        HollowKnight = 1,
        SealedSiblings = 2,
        Radiance = 3,
        Godhome = 4,
        GodhomeFlower = 5,
        GrubHunt = 6,
        MAX = GrubHunt
    }

    public abstract class Goal
    {
        public abstract string Name { get; }
        public abstract string Description { get; }

        private static readonly Dictionary<GoalsLookup, Goal> Lookup = new()
        {
            [GoalsLookup.HollowKnight] = new HollowKnightGoal(),
            [GoalsLookup.SealedSiblings] = new SealedSiblingsGoal(),
            [GoalsLookup.Radiance] = new RadianceGoal(),
            [GoalsLookup.Godhome] = new GodhomeGoal(),
            [GoalsLookup.GodhomeFlower] = new GodhomeFlowerGoal(),
            [GoalsLookup.GrubHunt] = new GrubHuntGoal(),
        };

        static Goal()
        {
            Lookup[GoalsLookup.Any] = new AnyGoal(Lookup.Values.ToList());
        }

        protected void FountainPlaqueTopEdit(ref string s) => s = "Your goal is";
        protected void FountainPlaqueNameEdit(ref string s) => s = Name;
        protected void FountainPlaqueDescEdit(ref string s) => s = Description;

        protected abstract bool VictoryCondition();

        public static Goal GetGoal(GoalsLookup key)
        {
            Goal value;
            if (Lookup.TryGetValue(key, out value))
            {
                return value;
            }
            ArchipelagoMod.Instance.LogError($"Listed goal is {key}, which is greater than {GoalsLookup.MAX}. Is this an outdated client?");
            throw new ArgumentOutOfRangeException($"Unrecognized goal condition {key} (are you running an outdated client?)");
        }

        public async Task CheckForVictoryAsync()
        {
            ArchipelagoMod.Instance.LogDebug($"Checking for victory; goal is {this.Name}; scene " +
                $"{UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");
            if (VictoryCondition())
            {
                ArchipelagoMod.Instance.LogDebug($"Victory detected, declaring!");
                try
                {
                    await ItemChangerMod.Modules.Get<GoalModule>().DeclareVictoryAsync().TimeoutAfter(1000);
                }
                catch (Exception ex) when (ex is TimeoutException or ArchipelagoSocketClosedException)
                {
                    ArchipelagoMod.Instance.LogError("Failed to send goal to server");
                    ArchipelagoMod.Instance.LogError(ex);
                }
            }
        }

        public void Select()
        {
            Events.AddLanguageEdit(new LanguageKey("Prompts", "FOUNTAIN_PLAQUE_TOP"), FountainPlaqueTopEdit);
            Events.AddLanguageEdit(new LanguageKey("Prompts", "FOUNTAIN_PLAQUE_MAIN"), FountainPlaqueNameEdit);
            Events.AddLanguageEdit(new LanguageKey("Prompts", "FOUNTAIN_PLAQUE_DESC"), FountainPlaqueDescEdit);
            OnSelected();
        }

        public void Deselect()
        {
            Events.RemoveLanguageEdit(new LanguageKey("Prompts", "FOUNTAIN_PLAQUE_TOP"), FountainPlaqueTopEdit);
            Events.RemoveLanguageEdit(new LanguageKey("Prompts", "FOUNTAIN_PLAQUE_MAIN"), FountainPlaqueNameEdit);
            Events.RemoveLanguageEdit(new LanguageKey("Prompts", "FOUNTAIN_PLAQUE_DESC"), FountainPlaqueDescEdit);
            OnDeselected();
        }

        public abstract void OnSelected();
        public abstract void OnDeselected();
    }

    public class AnyGoal : Goal
    {
        private IReadOnlyList<Goal> subgoals;

        public override string Name => "Any Goal";

        public override string Description => "Do whichever goal you like. If you're not sure,<br>try defeating the Hollow Knight!";

        public AnyGoal(IReadOnlyList<Goal> subgoals)
        {
            this.subgoals = subgoals;
        }

        public override void OnSelected()
        {
            foreach (Goal goal in subgoals)
            {
                goal.OnSelected();
            }
        }

        public override void OnDeselected()
        {
            foreach (Goal goal in subgoals)
            {
                goal.OnDeselected();
            }
        }

        protected override bool VictoryCondition()
        {
            // this goal is never completed on its own, it relies on subgoals to check for victory themselves.
            throw new NotImplementedException();
        }
    }

    public abstract class EndingGoal : Goal
    {
        private static List<string> VictoryScenes = new()
        {
            SceneNames.Cinematic_Ending_A,   // THK
            SceneNames.Cinematic_Ending_B,   // Sealed Siblings
            SceneNames.Cinematic_Ending_C,   // Radiance
            "Cinematic_Ending_D",            // Godhome no flower quest
            SceneNames.Cinematic_Ending_E    // Godhome w/ flower quest
        };

        public abstract string MinimumGoalScene { get; }

        public override void OnSelected()
        {
            Events.OnSceneChange += SceneChanged;
        }

        public override void OnDeselected()
        {
            Events.OnSceneChange -= SceneChanged;
        }

        private async void SceneChanged(UnityEngine.SceneManagement.Scene obj)
        {
            await CheckForVictoryAsync();
        }

        protected override bool VictoryCondition()
        {
            string activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (activeScene.StartsWith("Cinematic_Ending_"))
            {
                int minGoalSceneIndex = VictoryScenes.IndexOf(MinimumGoalScene);
                int sceneIndex = VictoryScenes.IndexOf(activeScene);
                return sceneIndex >= minGoalSceneIndex;
            }
            return false;
        }

    }

    public class HollowKnightGoal : EndingGoal
    {
        public override string Name => "The Hollow Knight";
        public override string Description => "Defeat The Hollow Knight<br>or any harder ending.";
        public override string MinimumGoalScene => SceneNames.Cinematic_Ending_A;
    }

    public class SealedSiblingsGoal : EndingGoal
    {
        public override string Name => "Sealed Siblings";
        public override string Description => "Complete the Sealed Siblings ending<br>or any harder ending.";
        public override string MinimumGoalScene => SceneNames.Cinematic_Ending_B;
    }

    public class RadianceGoal : EndingGoal
    {
        public override string Name => "Dream No More";
        public override string Description => "Defeat The Radiance in Black Egg Temple<br>or Absolute Radiance in Pantheon 5.";
        public override string MinimumGoalScene => SceneNames.Cinematic_Ending_C;
    }

    public class GodhomeGoal : EndingGoal
    {
        public override string Name => "Embrace the Void";
        public override string Description => "Defeat Absolute Radiance in Pantheon 5.";
        public override string MinimumGoalScene => "Cinematic_Ending_D";
    }

    public class GodhomeFlowerGoal : EndingGoal
    {
        public override string Name => "Delicate Flower";
        public override string Description => "Defeat Absolute Radiance in Pantheon 5<br>after delivering the flower to the Godseeker.";
        public override string MinimumGoalScene => SceneNames.Cinematic_Ending_E;
    }

    public class GrubHuntGoal : Goal
    {
        public override string Name => "Grub Hunt";

        public override string Description => $"Save {ArchipelagoMod.Instance.GrubHuntRequiredGrubs} of your Grubs.";

        private static readonly MethodInfo setIntInternal = typeof(PlayerData).GetMethod("SetIntInternal");
        private Hook onSetIntInternal;
        
        public override void OnSelected()
        {
            if (onSetIntInternal == null)
            {
                onSetIntInternal = new Hook(setIntInternal, OnSetPlayerInt);
            }
            else
            {
                onSetIntInternal.Apply();
            }
        }

        public override void OnDeselected()
        {
            onSetIntInternal.Undo();
        }

        protected override bool VictoryCondition()
        {
            return PlayerData.instance.grubsCollected >= ArchipelagoMod.Instance.GrubHuntRequiredGrubs;
        }

        private async void OnSetPlayerInt(On.PlayerData.orig_SetInt orig, PlayerData self, string intName, int value)
        {
            orig(self, intName, value);
            if (intName == nameof(PlayerData.grubsCollected))
            {
                await CheckForVictoryAsync();
            }
        }
    }
}
