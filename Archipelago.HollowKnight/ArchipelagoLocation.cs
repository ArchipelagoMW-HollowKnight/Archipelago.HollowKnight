using ItemChanger;
using ItemChanger.Locations;
using ItemChanger.Placements;
using ItemChanger.Tags;

namespace Archipelago.HollowKnight
{
    public class ArchipelagoLocation : AutoLocation
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
            AutoPlacement pmt = new AutoPlacement(name)
            {
                Location = this
            };

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