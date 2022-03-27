using ItemChanger;
using ItemChanger.Tags;

namespace Archipelago.HollowKnight
{
    public class ArchipelagoItem : AbstractItem
    {
        public ArchipelagoItem(string name)
        {
            this.name = name;
            UIDef = new ItemChanger.UIDefs.MsgUIDef()
            {
                name = new BoxedString(this.name),
                shopDesc = new BoxedString("This looks important, assuming beating the game is important to you."),
                sprite = new BoxedSprite(Archipelago.SmallSprite)
            };
            InteropTag tag = AddTag<InteropTag>();
            tag.Message = "RecentItems";
            tag.Properties["DisplayMessage"] = $"{this.name}\nsent to the multiworld.";
        }

        public override void GiveImmediate(GiveInfo info)
        {
            ItemChanger.Internal.MessageController.Enqueue(Archipelago.Sprite, name);
        }
    }
}