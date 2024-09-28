using Archipelago.HollowKnight.IC.Modules;
using ItemChanger;

namespace Archipelago.HollowKnight.IC.Items
{
    public class GoalItem : AbstractItem
    {
        private GoalModule goalModule;

        protected override void OnLoad()
        {
            goalModule = ItemChangerMod.Modules.Get<GoalModule>();
        }

        public override async void GiveImmediate(GiveInfo info)
        {
            await goalModule.DeclareVictoryAsync();
        }
    }
}
