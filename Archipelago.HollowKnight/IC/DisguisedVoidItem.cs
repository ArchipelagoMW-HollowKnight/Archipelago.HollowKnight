using ItemChanger;
using ItemChanger.Tags;

namespace Archipelago.HollowKnight.IC
{
    internal class DisguisedVoidItem : AbstractItem
    {
        public DisguisedVoidItem(AbstractItem originalItem)
        {
            name = originalItem.name;
            UIDef = new ArchipelagoUIDef(originalItem.UIDef);

            InteropTag tag = AddTag<InteropTag>();
            tag.Message = "RecentItems";
            tag.Properties["IgnoreItem"] = true;
            //tag.Properties["DisplayMessage"] = $"{this.name}";
        }

        public override void GiveImmediate(GiveInfo info)
        {
            ItemChanger.Internal.MessageController.Enqueue(UIDef.GetSprite(), name);
        }
    }
}
