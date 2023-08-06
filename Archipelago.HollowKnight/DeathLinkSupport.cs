using Archipelago.HollowKnight.IC;
using Archipelago.MultiClient.Net.BounceFeatures.DeathLink;
using HutongGames.PlayMaker;
using ItemChanger;
using ItemChanger.Extensions;
using ItemChanger.FsmStateActions;
using Modding;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Archipelago.HollowKnight
{
    public static class DeathLinkMessages
    {
        public static readonly List<string> DefaultMessages = new()
        {
            "@ died.",
            "@ has perished.",
            "@ made poor life choices.",
            "@ didn't listen to Hornet's advice.",
            "@ took damage equal to or more than their current HP.",
            "@ made a fatal mistake.",
            "@ threw some shade at @.",
            "@ decided to set up a Shade Skip.", // A System of Vibrant Colors (edited)
            "Hopefully @ didn't have a fragile charm equipped.", // Koatlus
            "A true servant gives all for the Kingdom.  Let @ relieve you of your life.", // Koatlus
            "Through @'s sacrifice, you are now dead.", // Koatlus
            "The truce remains.  Our vigil holds.  @ must respawn.", // Koatlus
            "Hopefully @ didn't have a fragile charm equipped.", // Koatlus
        };

        public static readonly List<string> UnknownMessages = new()
        {
            "@ has died in a manner most unusual.",
            "@ found a way to break the game, and the game broke @ back.",
            "@ has lost The Game",
        };

        public static readonly Dictionary<int, List<string>> MessagesByType = new()
        {
            {
                1, // Deaths from enemy damage 
                new List<string>
                {
                    "@ has discovered that there are bugs in Hallownest.",
                    "@ should have dodged.",
                    "@ should have jumped.",
                    "@ significantly mistimed their parry attempt.",
                    "@ should have considered equipping Dreamshield.",
                    "@ must have never fought that enemy before.",
                    "@ did not make it to phase 2.",
                    "@ dashed in the wrong direction.", // Murphmario
                    "@ tried to talk it out.", // SnowOfAllTrades
                    "@ made masterful use of their vulnerability frames.",
                }
            },
            {
                2, // Deaths from spikes
                new List<string>
                {
                    "@ was in the wrong place.",
                    "@ mistimed their jump.",
                    "@ didn't see the sharp things.",
                    "@ didn't see that saw.",
                    "@ fought the spikes and the spikes won.",
                    "@ sought roses but found only thorns.",
                    "@ was pricked to death.", // A System of Vibrant Colors
                    "@ dashed in the wrong direction.", // Murphmario
                    "@ found their own Path of Pain.", // Fatman
                    "@ has strayed from the White King's roads.", // Koatlus
                }
            },
            {
                3, // Deaths from acid
                new List<string>
                {
                    "@ was in the wrong place.",
                    "@ mistimed their jump.",
                    "@ forgot their floaties.",
                    "What @ thought was H2O was H2SO4.",
                    "@ wishes they could swim.",
                    "@ used the wrong kind of dive.",
                    "@ got into a fight with a pool of liquid and lost.",
                    "@ forgot how to swim", // squidy
                }
            },
            {
                999, // Deaths in the dream realm
                new List<string>
                {
                    "@ dozed off for good.",
                    "@ was caught sleeping on the job.",
                    "@ sought dreams but found only nightmares.",
                    "@ got lost in Limbo.",
                    "Good night, @.",
                    "@ is resting in pieces.",
                    "@ exploded into a thousand pieces of essence.",
                    "Hey, @, you're finally awake.",
                }
            },
        };

        private static readonly Random random = new(); // This is only messaging, so does not need to be seeded.

        public static string GetDeathMessage(int cause, string player)
        {
            // Build candidate death messages.
            List<string> messages;
            bool knownCauseOfDeath = DeathLinkMessages.MessagesByType.TryGetValue(cause, out messages);

            if (knownCauseOfDeath)
            {
                messages = new(messages);
                messages.AddRange(DeathLinkMessages.DefaultMessages);
            }
            else
            {
                messages = DeathLinkMessages.UnknownMessages;
            }

            // Choose one at random
            string message = messages[random.Next(0, messages.Count)].Replace("@", player);

            // If it's an unknown death, tag in some debugging info
            if (!knownCauseOfDeath)
            {
                Archipelago.Instance.LogWarn($"UNKNOWN cause of death {cause}");
                message += $" (Type: {cause})";
            }

            return message;
        }
    };

    public class DeathLinkSupport
    {
        public const string AMNESTY_VARIABLE_NAME = "Deathlink Amnesty";
        private static readonly MethodInfo HeroController_CanTakeDamage = typeof(HeroController)
            .GetMethod("CanTakeDamage", BindingFlags.NonPublic | BindingFlags.Instance);

        public static readonly DeathLinkSupport Instance = new();
        public bool Enabled { get; private set; } = false;

        private DeathLinkService service = null;
        private DeathLinkType mode => Archipelago.Instance.SlotOptions.DeathLink;
        private DeathLinkStatus status;
        private int lastDamageType;
        private DateTime lastDamageTime;
        private bool hasEditedFsm = false;

        private DeathLinkSupport()
        {
            Reset();
        }

        private void Reset()
        {
            lastDamageType = 0;
            lastDamageTime = DateTime.MinValue;
            status = DeathLinkStatus.None;
        }

        public void Enable()
        {
            Archipelago ap = Archipelago.Instance;
            if (Enabled)
            {
                ap.LogWarn("Tried to enable already-enabled DeathlinkSupport; disabling first.");
                Disable();
            }

            Enabled = true;
            Reset();

            ap.LogDebug($"Enabling DeathLink support, type: {mode}");
            service = ap.session.CreateDeathLinkService();
            service.EnableDeathLink();
            service.OnDeathLinkReceived += OnDeathLinkReceived;
            ModHooks.HeroUpdateHook += OnHeroUpdate;
            On.HeroController.TakeDamage += OnTakeDamage;
            ItemChanger.Events.AddFsmEdit(new FsmID("Hero Death Anim"), FsmEdit);
        }

        public void Disable()
        {
            Archipelago.Instance.LogDebug("Disabling DeathLink support.");
            if (!Enabled)
            {
                return;
            }

            Reset();

            if (service != null)
            {
                service.OnDeathLinkReceived -= OnDeathLinkReceived;
                service = null;
            }

            Enabled = false;
            ModHooks.HeroUpdateHook -= OnHeroUpdate;
            On.HeroController.TakeDamage -= OnTakeDamage;
            ItemChanger.Events.RemoveFsmEdit(new FsmID("Hero Death Anim"), FsmEdit);
            hasEditedFsm = false;
        }

        private void FsmEdit(PlayMakerFSM fsm)
        {
            if (hasEditedFsm)
            {
                return;
            }
            hasEditedFsm = true;

            Archipelago ap = Archipelago.Instance;

            FsmBool amnesty = fsm.AddFsmBool(AMNESTY_VARIABLE_NAME, false);
            // Death animation starts here - normally whether you get a shade or not is determined purely by whether
            // you're in a dream or not.
            FsmState mapZone = fsm.GetState("Map Zone");

            // We patch this for most of our logic, as well as a short-circuit past all of the FSM logic for shade creation, charm breakage, etc.
            mapZone.AddFirstAction(new Lambda(() =>
            {
                ap.LogDebug($"FsmEdit Pre: Status={status}  Mode={mode}.");

                if (status != DeathLinkStatus.Dying)
                { 
                    ap.LogDebug($"FsmEdit Pre: Not a deathlink death, so sending out our own deathlink.");
                    // If we're not caused by DeathLink... then we send a DeathLink
                    SendDeathLink();
                    return;
                }
                else
                {
                    ap.LogDebug("Beginning deathlink death handling");
                }

                amnesty.Value = !(
                    mode == DeathLinkType.Vanilla
                    || (mode == DeathLinkType.Shade && PlayerData.instance.shadeScene == "None")
                );

                if (!amnesty.Value)
                {
                    ap.LogDebug($"FsmEdit Pre: Ineligible for amnesty.");
                }
            }));
            mapZone.AddLastAction(new Lambda(() =>
            {
                if (amnesty.Value)
                {
                    ap.LogDebug($"FsmEdit Post: Amnesty activated, triggering events");
                    fsm.SetState("Save");
                }
            }));

            // End of vanilla deaths is... fun.
            FsmState deathEnding = fsm.GetState("End");

            // push this to a later step
            fsm.GetState("Limit Soul?").Actions = new FsmStateAction[] { };

            // Replace the first two action (which normally start the soul limiter and notify about it)
            deathEnding.Actions[0] = new Lambda(() =>
            {
                // Mimic the Limit Soul? state and the action being replaced - we only want to soul limit if the
                // player spawned a shade
                if (!amnesty.Value)
                {
                    fsm.Fsm.BroadcastEvent("SOUL LIMITER UP");
                    GameManager.instance.StartSoulLimiter();
                }
            });

            // the following 3 states are the ending states of each branch of the FSM. we'll link them into a custom state that resets
            // deathlink for us
            FsmState dreamReturn = fsm.GetState("Dream Return");
            FsmState waitForHeroController = fsm.GetState("Wait for HeroController");
            FsmState steelSoulCheck = fsm.GetState("Shade?");
            FsmState[] endingStates = new[] { dreamReturn, waitForHeroController, steelSoulCheck };
            // add deathlink cleanup state
            FsmState cleanupDeathlink = fsm.AddState("Cleanup Deathlink");
            cleanupDeathlink.AddFirstAction(new Lambda(() =>
            {
                ap.LogDebug("Resetting deathlink state");
                amnesty.Value = false;
                status = DeathLinkStatus.None;
            }));
            foreach (FsmState state in endingStates)
            {
                state.AddTransition("FINISHED", cleanupDeathlink);
            }
        }

        public void MurderPlayer()
        {
            string scene = GameManager.instance.sceneName;
            Archipelago.Instance.LogDebug($"Deathlink-initiated kill starting. Current scene: {scene}");
            status = DeathLinkStatus.Dying;
            HeroController.instance.TakeDamage(HeroController.instance.gameObject, GlobalEnums.CollisionSide.other,
                9999, 0);
        }

        private void OnHeroUpdate()
        {
            HeroController hc = HeroController.instance;
            if (status == DeathLinkStatus.Pending 
                && hc.damageMode == GlobalEnums.DamageMode.FULL_DAMAGE
                && PlayerData.instance.GetInt(nameof(PlayerData.health)) > 0
                && (bool)HeroController_CanTakeDamage.Invoke(hc, null))
            {
                MurderPlayer();
            }
        }

        private void OnTakeDamage(On.HeroController.orig_TakeDamage orig, HeroController self,
            UnityEngine.GameObject go, GlobalEnums.CollisionSide damageSide, int damageAmount, int hazardType)
        {
            lastDamageTime = DateTime.UtcNow;
            lastDamageType = hazardType;
            orig(self, go, damageSide, damageAmount, hazardType);
        }

        public void SendDeathLink()
        {
            Archipelago ap = Archipelago.Instance;
            // Don't send death links if we're currently in the process of dying to another deathlink.
            if (status == DeathLinkStatus.Dying)
            {
                ap.LogDebug("SendDeathLink(): Not sending a deathlink because we're in the process of dying to one");
                return;
            }

            if (service == null || !Enabled)
            {
                ap.LogDebug("SendDeathLink(): Not sending a deathlink because not enabled.");
                return;
            }

            if ((DateTime.UtcNow - lastDamageTime).TotalSeconds > 5)
            {
                ap.LogWarn("Last damage was a long time ago, resetting damage type to zero.");
                // Damage source was more than 5 seconds ago, so ignore damage type
                lastDamageType = 0;
            }

            string message = DeathLinkMessages.GetDeathMessage(lastDamageType, Archipelago.Instance.Player);
            ap.LogDebug(
                $"SendDeathLink(): Sending deathlink.  \"{message}\"");
            service.SendDeathLink(new DeathLink(Archipelago.Instance.Player, message));
        }


        private void OnDeathLinkReceived(DeathLink deathLink)
        {
            Archipelago ap = Archipelago.Instance;
            ap.LogDebug($"OnDeathLinkReceived(): Receiving deathlink.  Status={status}.");

            if (status == DeathLinkStatus.None)
            {
                status = DeathLinkStatus.Pending;

                string cause = deathLink.Cause;
                if (cause == null || cause == "")
                {
                    cause = $"{deathLink.Source} died.";
                }

                MenuChanger.ThreadSupport.BeginInvoke(() =>
                {
                    new ItemChanger.UIDefs.MsgUIDef()
                    {
                        name = new BoxedString(cause),
                        sprite = new ArchipelagoSprite { key = "DeathLinkIcon" }
                    }.SendMessage(MessageType.Corner, null);
                });

                lastDamageType = 0;
            }
            else
            {
                ap.LogDebug("Skipping this deathlink as one is currently in progress");
            }
        }
    }
}