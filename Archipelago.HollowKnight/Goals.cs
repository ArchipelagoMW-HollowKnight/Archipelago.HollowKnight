using Archipelago.HollowKnight.IC;
using Archipelago.HollowKnight.IC.Items;
using Archipelago.HollowKnight.IC.Modules;
using Archipelago.MultiClient.Net.Exceptions;
using ItemChanger;
using ItemChanger.Extensions;
using ItemChanger.Internal;
using ItemChanger.Placements;
using System;
using System.Collections.Generic;
using System.Linq;
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
        public virtual bool CanBeSelectedForAnyGoal => true;

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
            foreach (Goal goal in subgoals.Where(g => g.CanBeSelectedForAnyGoal))
            {
                goal.OnSelected();
            }
        }

        public override void OnDeselected()
        {
            foreach (Goal goal in subgoals.Where(g => g.CanBeSelectedForAnyGoal))
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

    /// <summary>
    /// A goal which is achieved by completing a given ending (or harder)
    /// </summary>
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

    /// <summary>
    /// A goal which is achieved by obtaining the Victory item placed at a given location in the world
    /// </summary>
    public abstract class ItemGoal : Goal
    {
        // We don't use CheckForVictoryAsync here, rather, the Victory item uses GoalModule.DeclareVictoryAsync
        // directly when acquired.
        protected override bool VictoryCondition() => false;

        protected virtual string GetGoalItemName() => "Victory";
        protected abstract string GetGoalLocation();
        protected virtual Cost GetGoalCost() => null;
        protected virtual UIDef GetGoalUIDef() => new ArchipelagoUIDef()
        {
            name = new BoxedString(GetGoalItemName()),
            shopDesc = new BoxedString("You completed your goal so you should probably get this to flex on your friends."),
            sprite = new ArchipelagoSprite { key = "IconColorSmall" }
        };

        public override void OnSelected()
        {
            string goalLocation = GetGoalLocation();
            AbstractPlacement plt = Ref.Settings.Placements.GetOrDefault(goalLocation);
            if (plt == null)
            {
                plt = Finder.GetLocation(goalLocation).Wrap();
            }

            // don't duplicate the goal
            if (plt.Items.Any(i => i is GoalItem))
            {
                return;
            }

            AbstractItem item = new GoalItem()
            {
                name = GetGoalItemName(),
                UIDef = GetGoalUIDef(),
            };
            // modules (and therefore goals) are loaded prior to placements so nothing special needed
            // to make this load.
            plt.Add(item);

            // handle the cost
            if (plt is ISingleCostPlacement icsp)
            {
                Cost desiredCost = GetGoalCost();
                if (icsp.Cost == null)
                {
                    icsp.Cost = desiredCost;
                }
                else
                {
                    icsp.Cost += desiredCost;
                }
            }
            else
            {
                item.AddTag(new CostTag()
                {
                    Cost = GetGoalCost(),
                });
            }
        }

        public override void OnDeselected() { }
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

    public class GrubHuntGoal : ItemGoal
    {
        public override string Name => "Grub Hunt";

        public override string Description => $"Save {ArchipelagoMod.Instance.SlotData.GrubsRequired.Value} of your Grubs and visit Grubfather<br>to obtain happiness.";

        public override bool CanBeSelectedForAnyGoal => ArchipelagoMod.Instance.SlotData.GrubsRequired != null;

        protected override string GetGoalItemName() => "Happiness";
        protected override string GetGoalLocation() => LocationNames.Grubfather;
        protected override Cost GetGoalCost() => Cost.NewGrubCost(ArchipelagoMod.Instance.SlotData.GrubsRequired.Value);
        protected override UIDef GetGoalUIDef() => new ArchipelagoUIDef()
        {
            name = new BoxedString("Happiness"),
            shopDesc = new BoxedString("Meemawmaw! Meemawmaw!"),
            sprite = new ArchipelagoSprite { key = "GrubHappyv2" }
        };
    }
}
