using ItemChanger;
using ItemChanger.Extensions;
using ItemChanger.Internal;
using ItemChanger.Tags;
using Newtonsoft.Json;

namespace Archipelago.HollowKnight.IC
{
    public class RemotePlacement : AbstractPlacement
    {
        public const string SINGLETON_NAME = "Remote_Items";

        [JsonConstructor]
        private RemotePlacement(string Name) : base(SINGLETON_NAME) { }

        public static RemotePlacement GetOrAddSingleton()
        {
            if (!Ref.Settings.Placements.TryGetValue(SINGLETON_NAME, out AbstractPlacement pmt))
            {
                pmt = new RemotePlacement(SINGLETON_NAME);
                CompletionWeightTag remoteCompletionWeightTag = pmt.AddTag<CompletionWeightTag>();
                remoteCompletionWeightTag.Weight = 0;
                InteropTag pinTag = new()
                {
                    Message = "RandoSupplementalMetadata",
                    Properties = new()
                    {
                        ["DoNotMakePin"] = true,
                    }
                };
                pmt.AddTag(pinTag);
                ItemChangerMod.AddPlacements(pmt.Yield());
            }
            return (RemotePlacement)pmt;
        }

        protected override void OnLoad()
        {

        }

        protected override void OnUnload()
        {

        }
    }
}
