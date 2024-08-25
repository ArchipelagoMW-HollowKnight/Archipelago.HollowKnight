using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.BounceFeatures.DeathLink;
using HutongGames.PlayMaker;
using ItemChanger;
using ItemChanger.Extensions;
using ItemChanger.FsmStateActions;
using Modding;
using System;
using System.Reflection;

namespace Archipelago.HollowKnight.IC.Modules
{
    public class DeathLinkModule : ItemChanger.Modules.Module
    {
        public const string PREVENT_SHADE_VARIABLE_NAME = "Deathlink Prevent Shade";
        public const string IS_DEATHLINK_VARIABLE_NAME = "Is Deathlink Death";
        private static readonly MethodInfo HeroController_CanTakeDamage = typeof(HeroController)
            .GetMethod("CanTakeDamage", BindingFlags.NonPublic | BindingFlags.Instance);

        private DeathLinkService service = null;
        private DeathLinkShadeHandling shadeHandling => ArchipelagoMod.Instance.SlotData.Options.DeathLinkShade;
        private bool breakFragileCharms => ArchipelagoMod.Instance.SlotData.Options.DeathLinkBreaksFragileCharms;
        private DeathLinkStatus status;
        private int lastDamageType;
        private DateTime lastDamageTime;
        private bool hasEditedFsm = false;

        private ArchipelagoSession session;

        private void Reset()
        {
            lastDamageType = 0;
            lastDamageTime = DateTime.MinValue;
            status = DeathLinkStatus.None;
        }

        public override void Initialize()
        {
            session = ArchipelagoMod.Instance.session;
            Reset();

            ArchipelagoMod.Instance.LogDebug($"Enabling DeathLink support, type: {shadeHandling}");
            service = ArchipelagoMod.Instance.session.CreateDeathLinkService();
            service.EnableDeathLink();
            service.OnDeathLinkReceived += OnDeathLinkReceived;
            ModHooks.HeroUpdateHook += OnHeroUpdate;
            On.HeroController.TakeDamage += OnTakeDamage;
            Events.AddFsmEdit(new FsmID("Hero Death Anim"), FsmEdit);
        }

        public override void Unload()
        {
            Reset();

            if (service != null)
            {
                service.OnDeathLinkReceived -= OnDeathLinkReceived;
                service = null;
            }

            ModHooks.HeroUpdateHook -= OnHeroUpdate;
            On.HeroController.TakeDamage -= OnTakeDamage;
            Events.RemoveFsmEdit(new FsmID("Hero Death Anim"), FsmEdit);
            hasEditedFsm = false;
        }

        private void FsmEdit(PlayMakerFSM fsm)
        {
            if (hasEditedFsm)
            {
                return;
            }
            hasEditedFsm = true;

            ArchipelagoMod ap = ArchipelagoMod.Instance;

            FsmBool preventShade = fsm.AddFsmBool(PREVENT_SHADE_VARIABLE_NAME, false);
            FsmBool isDeathlink = fsm.AddFsmBool(IS_DEATHLINK_VARIABLE_NAME, false);
            // Death animation starts here - normally whether you get a shade or not is determined purely by whether
            // you're in a dream or not.
            FsmState mapZone = fsm.GetState("Map Zone");

            // If it's not someone else's death, then send out a deathlink. Also compute whether a shade should be spawned since
            // multiple other states need to know.
            mapZone.AddFirstAction(new Lambda(() =>
            {
                ap.LogDebug($"FsmEdit Pre: Status={status} Shade handling={shadeHandling} Break fragiles={breakFragileCharms}.");

                bool isDeathlinkDeath = status == DeathLinkStatus.Dying;

                if (!isDeathlinkDeath)
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

                isDeathlink.Value = isDeathlinkDeath;
                preventShade.Value = !(
                    shadeHandling == DeathLinkShadeHandling.Vanilla
                    || shadeHandling == DeathLinkShadeHandling.Shade && PlayerData.instance.shadeScene == "None"
                );

                if (!preventShade.Value)
                {
                    ap.LogDebug($"FsmEdit Pre: Shade will be created.");
                }
            }));

            // route around penalties based on settings
            FsmState breakMsg = fsm.GetState("Break Msg");
            FsmState removeOvercharm = fsm.GetState("Remove Overcharm");

            FsmState createShadeCheck = fsm.AddState("Create Shade?");
            createShadeCheck.AddLastAction(new DelegateBoolTest(() => isDeathlink.Value, null, "FINISHED"));
            createShadeCheck.AddLastAction(new DelegateBoolTest(() => preventShade.Value, "SKIP SHADE", null));
            createShadeCheck.AddTransition("SKIP SHADE", "Save");
            createShadeCheck.AddTransition("FINISHED", "Remove Geo");

            FsmState breakFragilesCheck = fsm.AddState("Break Fragiles?");
            breakFragilesCheck.AddLastAction(new DelegateBoolTest(() => isDeathlink.Value, null, "FINISHED"));
            breakFragilesCheck.AddLastAction(new DelegateBoolTest(() => !breakFragileCharms, "SKIP BREAK", null));
            breakFragilesCheck.AddTransition("SKIP BREAK", createShadeCheck);
            breakFragilesCheck.AddTransition("FINISHED", "Break Glass HP");

            mapZone.RemoveTransitionsOn("FINISHED");
            mapZone.AddTransition("FINISHED", breakFragilesCheck);

            breakMsg.RemoveTransitionsOn("FINISHED");
            breakMsg.AddTransition("FINISHED", createShadeCheck);
            removeOvercharm.RemoveTransitionsOn("FINISHED");
            removeOvercharm.AddTransition("FINISHED", createShadeCheck);

            // adjust soul limiter to be created only if a shade was created
            FsmState deathEnding = fsm.GetState("End");
            fsm.GetState("Limit Soul?").Actions = [];
            // Replace the first two action (which normally start the soul limiter and notify about it)
            deathEnding.Actions[0] = new Lambda(() =>
            {
                // Mimic the Limit Soul? state and the action being replaced - we only want to soul limit if the
                // player spawned a shade
                if (!preventShade.Value)
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
            FsmState[] endingStates = [dreamReturn, waitForHeroController, steelSoulCheck];
            // add deathlink cleanup state
            FsmState cleanupDeathlink = fsm.AddState("Cleanup Deathlink");
            cleanupDeathlink.AddFirstAction(new Lambda(() =>
            {
                ap.LogDebug("Resetting deathlink state");
                preventShade.Value = false;
                isDeathlink.Value = false;
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
            ArchipelagoMod.Instance.LogDebug($"Deathlink-initiated kill starting. Current scene: {scene}");
            status = DeathLinkStatus.Dying;
            HeroController.instance.TakeDamage(HeroController.instance.gameObject, GlobalEnums.CollisionSide.other,
                9999, 0);
        }

        private void OnHeroUpdate()
        {
            HeroController hc = HeroController.instance;
            if (status == DeathLinkStatus.Pending
                && hc.acceptingInput
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
            ArchipelagoMod ap = ArchipelagoMod.Instance;
            // Don't send death links if we're currently in the process of dying to another deathlink.
            if (status == DeathLinkStatus.Dying)
            {
                ap.LogDebug("SendDeathLink(): Not sending a deathlink because we're in the process of dying to one");
                return;
            }

            if (service == null)
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

            string message = DeathLinkMessages.GetDeathMessage(lastDamageType, ArchipelagoMod.Instance.session.Players.ActivePlayer.Alias);
            ap.LogDebug(
                $"SendDeathLink(): Sending deathlink.  \"{message}\"");
            service.SendDeathLink(new DeathLink(ArchipelagoMod.Instance.session.Players.ActivePlayer.Alias, message));
        }

        private void OnDeathLinkReceived(DeathLink deathLink)
        {
            ArchipelagoMod ap = ArchipelagoMod.Instance;
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
