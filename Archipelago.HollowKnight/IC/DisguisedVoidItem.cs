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
        }

        public override void GiveImmediate(GiveInfo info)
        {
            
        }
    }
}
