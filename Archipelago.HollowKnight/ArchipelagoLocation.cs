using ItemChanger;
using ItemChanger.Locations;
using ItemChanger.Placements;
using ItemChanger.Tags;

namespace Archipelago.HollowKnight
{
    public class ArchipelagoLocation : AutoLocation
    {

        //public override GiveInfo GetGiveInfo()
        //{
        //    return new GiveInfo
        //    {
        //        FlingType = FlingType.DirectDeposit,
        //        Callback = (t) => Archipelago.Instance.LogDebug($"Picked up item from AP location: {t.GetPreviewName()}"),
        //        Container = Container.Chest,
        //        MessageType = MessageType.Corner,
        //    };
        //}

        public ArchipelagoLocation(string name)
        {
            this.name = name;
            sceneName = null;
        }

        public override AbstractPlacement Wrap()
        {
            AutoPlacement pmt = new AutoPlacement(name)
            {
                Location = this
            };

            InteropTag tag = pmt.AddTag<InteropTag>();
            tag.Message = "RecentItems";
            tag.Properties["DisplaySource"] = name;
            return pmt;
        }

        protected override void OnLoad()
        {
            // noop for now
            // throw new NotImplementedException();
        }

        protected override void OnUnload()
        {
            // noop for now
            // throw new NotImplementedException();
        }
    }
}