using ItemChanger;
using ItemChanger.UIDefs;

namespace Archipelago.HollowKnight.IC
{
    internal class ArchipelagoUIDef : MsgUIDef
    {
        public ArchipelagoUIDef(UIDef def, string targetSlotName = null)
        {
            if (def is MsgUIDef msgDef)
            {
                shopDesc = msgDef.shopDesc.Clone();
                sprite = msgDef.sprite.Clone();
            }
            else
            {
                shopDesc = new BoxedString(def.GetShopDesc());
                sprite = new EmptySprite();
            }
            if (targetSlotName == null)
            {
                name = new BoxedString(def.GetPreviewName());
            }
            else
            {
                name = new BoxedString($"{targetSlotName}'s {def.GetPreviewName()}");
            }
        }
    }
}
