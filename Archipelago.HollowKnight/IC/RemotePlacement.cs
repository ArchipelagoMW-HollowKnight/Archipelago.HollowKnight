using ItemChanger;
using ItemChanger.Extensions;
using ItemChanger.Internal;
using ItemChanger.Tags;

namespace Archipelago.HollowKnight.IC
{
    public class RemotePlacement : AbstractPlacement
    {
        private const string SINGLETON_NAME = "Remote_Items";

        public RemotePlacement() : base(SINGLETON_NAME)
        {
        }

        public static RemotePlacement GetOrAddSingleton()
        {
            if (!Ref.Settings.Placements.TryGetValue(SINGLETON_NAME, out AbstractPlacement pmt))
            {
                pmt = new RemotePlacement();
                CompletionWeightTag remoteCompletionWeightTag = pmt.AddTag<CompletionWeightTag>();
                remoteCompletionWeightTag.Weight = 0;
                ItemChangerMod.AddPlacements(pmt.Yield());
            }
            return (RemotePlacement) pmt;
        }

        protected override void OnLoad()
        {

        }

        protected override void OnUnload()
        {

        }
    }
}
