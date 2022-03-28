using ItemChanger;
using ItemChanger.Tags;

namespace Archipelago.HollowKnight.IC
{
    public class ArchipelagoLocation : AbstractLocation
    {
        private readonly string displayMessage;

        public ArchipelagoLocation(string name, string displayMessage = null)
        {
            this.name = name;
            sceneName = null;
            this.displayMessage = displayMessage;
        }

        public override AbstractPlacement Wrap()
        {
            var pmt = new ArchipelagoPlacement(name);

            if (!string.IsNullOrWhiteSpace(displayMessage))
            {
                InteropTag tag = pmt.AddTag<InteropTag>();
                tag.Message = "RecentItems";
                tag.Properties["DisplayMessage"] = displayMessage;
            }
            return pmt;
        }

        protected override void OnLoad()
        {

        }

        protected override void OnUnload()
        {

        }
    }
}