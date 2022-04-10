using ItemChanger;
using ItemChanger.Tags;

namespace Archipelago.HollowKnight.IC
{
    internal class DisguisedVoidItem : AbstractItem
    {
        public DisguisedVoidItem(AbstractItem originalItem, string targetSlotName = null)
        {
            name = originalItem.name;
            UIDef = new ArchipelagoUIDef(originalItem.UIDef, targetSlotName);

            InteropTag tag = AddTag<InteropTag>();
            tag.Message = "RecentItems";
            if (!string.IsNullOrEmpty(targetSlotName))
            {
                tag.Properties["DisplayMessage"] = $"{originalItem.GetPreviewName()}\nsent to {targetSlotName}.";
            }
            else
            {
                tag.Properties["DisplayMessage"] = $"{originalItem.GetPreviewName()}\nsent to the multiworld.";
            }
        }

        public override void GiveImmediate(GiveInfo info)
        {
            
        }
    }
}
