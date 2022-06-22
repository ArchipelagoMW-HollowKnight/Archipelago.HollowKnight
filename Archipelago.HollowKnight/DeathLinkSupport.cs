using Archipelago.MultiClient.Net.BounceFeatures.DeathLink;
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
    };

    public class DeathLinkSupport
    {
        public readonly static DeathLinkSupport Instance = new();
        private DeathLinkService service = null;
        public bool Enabled { get; private set; } = false;
        private DeathLinkType Mode => Archipelago.Instance.SlotOptions.DeathLink;
        private readonly System.Random random = new();  // This is only messaging, so does not need to be seeded.

        private DeathLinkStatus Status;
        private int outgoingDeathlinks;
        private int lastDamageType;
        private DateTime lastDamageTime;
        private ShadeInfo shade;

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
            shade = null;
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
            ModHooks.BeforePlayerDeadHook += SendDeathLink;
            ModHooks.AfterPlayerDeadHook += ModHooks_AfterPlayerDeadHook;
            On.HeroController.TakeDamage += HeroController_TakeDamage;
            ItemChanger.Events.AddFsmEdit(new FsmID("Hero Death Anim"), FsmEdit);
            // SetupDreamDeathFSMOverride(true);
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
            ModHooks.BeforePlayerDeadHook -= SendDeathLink;
            ModHooks.AfterPlayerDeadHook -= ModHooks_AfterPlayerDeadHook;
            On.HeroController.TakeDamage -= HeroController_TakeDamage;
            ItemChanger.Events.RemoveFsmEdit(new FsmID("Hero Death Anim"), FsmEdit);
            // SetupDreamDeathFSMOverride(false);
        }


        private void FsmEdit(PlayMakerFSM obj)
        {
            // Edit hero dream deaths to trigger deathlink.
            var state = obj.Fsm.GetState("Anim Start");
            state.Actions = state.Actions.Append<FsmStateAction>(new ItemChanger.FsmStateActions.Lambda(() =>
            {
                lastDamageType = 999;  // Dream death messaging.
                SendDeathLink();
            })).ToArray();
        }


        /// <summary>
        /// Returns True if it is safe to kill the current player -- i.e. they can take damage, have character control, and are not in an unsafe scene.
        /// </summary>
        public bool CanMurderPlayer()
        {
            HeroController hc = HeroController.instance;
            return hc.acceptingInput && hc.damageMode == GlobalEnums.DamageMode.FULL_DAMAGE;
        }

        /// <summary>
        /// For easier DeathLink modes, we save/restore the player's previous shade along with other shenanigans.
        /// 
        /// In some cases, we skip this and allow a death to trigger normally -- e.g. dream sequences where no shade is spawned.
        /// In some cases, we force this even for harder modes -- e.g. rooms where shade creation is unsafe.
        /// </summary>
        public bool ShouldShadeSwap()
        {
            string scene = GameManager.instance.sceneName;
            string zone = GameManager.instance.GetCurrentMapZone();
            return (!(
                // These are all conditions where the death should be processed like a vanilla death with no shade switcharoo
                Mode == DeathLinkType.Vanilla
                || (Mode == DeathLinkType.Shade && PlayerData.instance.shadeScene == "None")
                || zone == "DREAM_WORLD" || zone == "GODS_GLORY" || zone == "WHITE_PALACE"  // Most dream sequences, except Radiance
            ));
        }

        public void MurderPlayer()
        {
            string scene = GameManager.instance.sceneName;
            Archipelago.Instance.LogDebug($"So, somebody else has chosen... death.  Current scene: {scene}");

            if(ShouldShadeSwap())
            {
                // Do the shade switcharoo
                Status = DeathLinkStatus.Dying;
                shade = ShadeInfo.FromPlayerData();
            }
            else
            {
                Status = DeathLinkStatus.None;
            }
            // Murder.
            HeroController.instance.TakeDamage(HeroController.instance.gameObject, GlobalEnums.CollisionSide.other, 9999, 0);
        }

        private void ModHooks_HeroUpdateHook()
        {
            if (Status == DeathLinkStatus.Pending && CanMurderPlayer())
            {
                MurderPlayer();
            }
        }

        private void ModHooks_AfterPlayerDeadHook()
        {
            var ap = Archipelago.Instance;
            if (Status != DeathLinkStatus.Dying)
            {
                Status = DeathLinkStatus.None;
                shade = ShadeInfo.FromPlayerData();
                if (shade != null)
                {
                    ap.LogDebug($"Detected normal death at scene {shade.Scene}.  Lost {shade.Geo} geo.");
                }
                return;
            }
            Status = DeathLinkStatus.None;
            PlayerData pd = PlayerData.instance;
            ap.LogDebug($"Dying due to deathlink.  Recovering {pd.geoPool} lost geo.");
            HeroController.instance.AddGeoQuietly(pd.geoPool);
            pd.geoPool = 0;
            if (shade == null || shade.Scene == "None")
            {
                ap.LogDebug("No saved shade info, so deleting the new one.");
                GameManager.instance.EndSoulLimiter();
                PlayerData.instance.soulLimited = false;
            }
            else
            {
                ap.LogDebug("Had saved shade data, so restoring it.");
                ap.LogDebug($"Respawning shade at {shade.Scene} with {shade.Geo} geo.");
                shade.WritePlayerData();
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
            // Don't send death links if we're currently in the process of dying to another deathlink.
            if (Status == DeathLinkStatus.Dying)
            {
                return;
            }
            if (service == null || !Enabled)
            {
                return;
            }

            if ((DateTime.UtcNow - lastDamageTime).TotalSeconds > 5)
            {
                Archipelago.Instance.LogWarn("Last damage was a long time ago, resetting damage type to zero.");
                // Damage source was more than 5 seconds ago, so ignore damage type
                lastDamageType = 0;
            }

            // Build candidate death messages.
            List<string> messages = null;
            bool knownCauseOfDeath = DeathLinkMessages.MessagesByType.TryGetValue(lastDamageType, out messages);

            if(knownCauseOfDeath)
            {
                messages = new(messages);
                messages.AddRange(DeathLinkMessages.DefaultMessages);
            } else
            {
                messages = DeathLinkMessages.UnknownMessages;
            }

            // Choose one at random
            string message = messages[random.Next(0, messages.Count)].Replace("@", Archipelago.Instance.Player);

            // If it's an unknown death, tag in some debugging info
            if (!knownCauseOfDeath)
            {
                Archipelago.Instance.LogWarn($"UNKNOWN cause of death {lastDamageType}");
                message += $" (Type: {lastDamageType})";
            }

            // Increment outgoing deathlinks and send the death.
            outgoingDeathlinks += 1;
            service.SendDeathLink(new(Archipelago.Instance.Player, message));
        }


        private void OnDeathLinkReceived(DeathLink deathLink)
        {
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
        }
    }
}
