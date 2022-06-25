﻿using Archipelago.MultiClient.Net.BounceFeatures.DeathLink;
using HutongGames.PlayMaker;
using ItemChanger;
using Modding;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Archipelago.HollowKnight
{
    public static class DeathLinkMessages
    {
        public readonly static List<string> DefaultMessages = new()
        {
            "@ died.",
            "@ has perished.",
            "@ made poor life choices.",
            "@ didn't listen to Hornet's advice.",
            "@ took damage equal to or more than their current HP.",
            "@ made a fatal mistake.",
            "@ threw some shade at @.",
        };

        public readonly static List<string> UnknownMessages = new()
        {
            "@ has died in a manner most unusual.",
            "@ found a way to break the game, and the game broke @ back.",
            "@ has lost The Game",
        };

        public readonly static Dictionary<int, List<string>> MessagesByType = new()
        {
            {
                1,  // Deaths from enemy damage 
                new List<string> {
                    "@ has discovered that there are bugs in Hallownest.",
                    "@ should have dodged.",
                    "@ should have jumped.",
                    "@ significantly mistimed their parry attempt.",
                    "@ should have considered equipping Dreamshield.",
                    "@ must have never fought that enemy before.",
                    "@ did not make it to phase 2.",
                }

            },
            {
                2,  // Deaths from spikes
                new List<string> {
                    "@ was in the wrong place.",
                    "@ mistimed their jump.",
                    "@ didn't see the sharp things.",
                    "@ didn't see that saw.",
                    "@ fought the spikes and the spikes won.",
                    "@ sought roses but found only thorns.",
                    "@ was pricked to death.",
                }
            },
            {
                3,  // Deaths from acid
                new List<string> {
                    "@ was in the wrong place.",
                    "@ mistimed their jump.",
                    "@ forgot their floaties.",
                    "What @ thought was H2O was H2SO4.",
                    "@ wishes they could swim.",
                    "@ used the wrong kind of dive.",
                    "@ got into a fight with a pool of liquid and lost.",
                }
            },
            {
                999,  // Deaths in the dream realm
                new List<string> {
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

        private readonly static Random random = new();  // This is only messaging, so does not need to be seeded.

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
        public readonly static DeathLinkSupport Instance = new();
        private DeathLinkService service = null;
        public bool Enabled { get; private set; } = false;
        private DeathLinkType Mode => Archipelago.Instance.SlotOptions.DeathLink;

        private DeathLinkStatus Status;
        private int outgoingDeathlinks;
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
            outgoingDeathlinks = 0;
            Status = DeathLinkStatus.None;
        }
        public void Enable()
        {
            var ap = Archipelago.Instance;
            if (Enabled)
            {
                ap.LogWarn("Tried to enable already-enabled DeathlinkSupport; disabling first.");
                Disable();
            }
            Enabled = true;
            Reset();

            ap.LogDebug($"Enabling DeathLink support, type: {Mode}");
            service = ap.session.CreateDeathLinkServiceAndEnable();
            service.OnDeathLinkReceived += OnDeathLinkReceived;
            ModHooks.HeroUpdateHook += ModHooks_HeroUpdateHook;
            On.HeroController.TakeDamage += HeroController_TakeDamage;
            ItemChanger.Events.AddFsmEdit(new FsmID("Hero Death Anim"), FsmEdit);
        }

        public void Disable()
        {
            var ap = Archipelago.Instance;
            ap.LogDebug("Disabling DeathLink support.");
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
            ModHooks.HeroUpdateHook -= ModHooks_HeroUpdateHook;
            On.HeroController.TakeDamage -= HeroController_TakeDamage;
            ItemChanger.Events.RemoveFsmEdit(new FsmID("Hero Death Anim"), FsmEdit);
            hasEditedFsm = false;
        }


        private FsmState PrependFSMAction(Fsm fsm, string name, Action action)
        {
            return PrependFSMAction(fsm.GetState(name), action);
        }

        private FsmState PrependFSMAction(FsmState state, Action action)
        {
            state.Actions = state.Actions.Prepend<FsmStateAction>(new ItemChanger.FsmStateActions.Lambda(action)).ToArray();
            return state;
        }

        private FsmState AppendFSMAction(Fsm fsm, string name, Action action)
        {
            return AppendFSMAction(fsm.GetState(name), action);
        }

        private FsmState AppendFSMAction(FsmState state, Action action)
        {
            state.Actions = state.Actions.Append<FsmStateAction>(new ItemChanger.FsmStateActions.Lambda(action)).ToArray();
            return state;
        }

        private void FsmEdit(PlayMakerFSM obj)
        {
            if (hasEditedFsm)
            {
                return;
            }
            hasEditedFsm = true;
            var ap = Archipelago.Instance;
            bool amnesty = false;  // Set True if death penalties should be prevented.

            Fsm fsm = obj.Fsm;
            FsmState fsmState;

            // Patch the Map Zone FSM for base Deathlink logic.  Oddly enough, this is the opening of the death FSM... not "Start"
            // We patch this for most of our logic, as well as a short-circuit past all of the FSM logic for shade creation, charm breakage, etc.
            fsmState = PrependFSMAction(fsm, "Map Zone", () =>
            {
                ap.LogDebug($"FsmEdit Pre: Status={Status}  Mode={Mode}.  Resetting status to None.");
                amnesty = (Status == DeathLinkStatus.Dying);

                if (!amnesty)
                {
                    {
                        ap.LogDebug($"FsmEdit Pre: Not a deathlink death, so sending out our own deathlink.");
                        // If we're not caused by DeathLink... then we send a DeathLink
                        SendDeathLink();
                        return;
                    }
                }

                amnesty = !(
                        Mode == DeathLinkType.Vanilla
                        || (Mode == DeathLinkType.Shade && PlayerData.instance.shadeScene == "None")
                );

                if (!amnesty)
                {
                    ap.LogDebug($"FsmEdit Pre: Ineligible for amnesty.");
                    return;
                }
            });
            AppendFSMAction(fsmState, () => {
                if (amnesty)
                {
                    ap.LogDebug($"FsmEdit Post: Amnesty activated, triggering events");
                    fsm.SetState("Save");
                }
            });

            Action clearDeathLink = () => { amnesty = false; Status = DeathLinkStatus.None; };

            // Near the end of dream deaths, clear DeathLinkStatus
            AppendFSMAction(fsm, "WP Check", clearDeathLink);

            // Soul Limiter gets set twice!  Just completely delete the first instance, all the time.
            fsm.GetState("Limit Soul?").Actions = new FsmStateAction[] { };

            // End of vanilla deaths is... fun.
            fsmState = fsm.GetState("End");

            // Replace the first two action (which normally start the soul limiter and notify about it)
            fsmState.Actions[0] = new ItemChanger.FsmStateActions.Lambda(() =>
            {
                // Mimic the former first two actions
                if (amnesty)
                {
                    amnesty = false;
                    return;
                }
                GameManager.instance.StartSoulLimiter();
                fsm.BroadcastEvent("SOUL LIMITER UP");
            });
            fsmState.Actions[1] = new ItemChanger.FsmStateActions.Lambda(clearDeathLink);
        }

        /// <summary>
        /// Returns True if it is safe to kill the current player -- i.e. they can take damage, have character control, and are not in an unsafe scene.
        /// </summary>
        public bool CanMurderPlayer()
        {
            HeroController hc = HeroController.instance;
            return hc.acceptingInput && hc.damageMode == GlobalEnums.DamageMode.FULL_DAMAGE && PlayerData.instance.health > 0;
        }

        public void MurderPlayer()
        {
            string scene = GameManager.instance.sceneName;
            Archipelago.Instance.LogDebug($"So, somebody else has chosen... death.  Current scene: {scene}");
            Status = DeathLinkStatus.Dying;
            HeroController.instance.TakeDamage(HeroController.instance.gameObject, GlobalEnums.CollisionSide.other, 9999, 0);
        }

        private void ModHooks_HeroUpdateHook()
        {
            if (Status == DeathLinkStatus.Pending && CanMurderPlayer())
            {
                MurderPlayer();
            }
        }

        private void HeroController_TakeDamage(On.HeroController.orig_TakeDamage orig, HeroController self, UnityEngine.GameObject go, GlobalEnums.CollisionSide damageSide, int damageAmount, int hazardType)
        {
            orig(self, go, damageSide, damageAmount, hazardType);
            lastDamageTime = DateTime.UtcNow;
            lastDamageType = hazardType;
        }

        public void SendDeathLink()
        {
            var ap = Archipelago.Instance;
            // Don't send death links if we're currently in the process of dying to another deathlink.
            if (Status == DeathLinkStatus.Dying)
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
            // Increment outgoing deathlinks and send the death.
            outgoingDeathlinks += 1;
            ap.LogDebug($"SendDeathLink(): Sending deathlink.  outgoingDeathLinks = {outgoingDeathlinks}.");
            service.SendDeathLink(new(Archipelago.Instance.Player, message));
        }


        private void OnDeathLinkReceived(DeathLink deathLink)
        {
            Archipelago.Instance.LogDebug($"OnDeathLinkReceived(): Receiving deathlink.  Status={Status}; outgoingDeathLinks = {outgoingDeathlinks}.");
            if (outgoingDeathlinks > 0)
            {
                outgoingDeathlinks--;
                return;
            }
            if (Status == DeathLinkStatus.None)
            {
                Status = DeathLinkStatus.Pending;
            }
            string cause = deathLink.Cause;
            if (cause == null || cause == "")
            {
                cause = $"{deathLink.Source} died.";
            }
            new ItemChanger.UIDefs.MsgUIDef()
            {
                name = new BoxedString(cause),
                sprite = new BoxedSprite(Archipelago.DeathLinkSprite)
            }.SendMessage(MessageType.Corner, null);
            lastDamageType = 0;
        }
    }
}
