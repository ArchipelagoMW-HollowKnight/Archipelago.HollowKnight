using ItemChanger;
using ItemChanger.Tags;

namespace Archipelago.HollowKnight
{
    public class ArchipelagoItem : AbstractItem
    {
        private readonly int locationId;

        public ArchipelagoItem(string name, int locationId)
        {
            this.name = name;
            UIDef = new ItemChanger.UIDefs.MsgUIDef()
            {
                name = new BoxedString(this.name),
                shopDesc = new BoxedString("This looks important. That is, assuming beating the game is important to you."),
                sprite = new BoxedSprite(Archipelago.Sprite)
            };
            InteropTag tag = AddTag<InteropTag>();
            tag.Message = "RecentItems";
            tag.Properties["IgnoreItem"] = true;
            this.locationId = locationId;
        }

        public override void GiveImmediate(GiveInfo info)
        {
            if (Archipelago.Instance.session != null && Archipelago.Instance.session.Socket.Connected)
            {
                Archipelago.Instance.session.Locations.CompleteLocationChecks(locationId);
            }
            ItemChanger.Internal.MessageController.Enqueue(Archipelago.Sprite, name);
        }
    }
}