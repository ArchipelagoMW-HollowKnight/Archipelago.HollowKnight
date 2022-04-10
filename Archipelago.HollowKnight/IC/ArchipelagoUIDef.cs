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
                name = string.IsNullOrEmpty(targetSlotName) ? msgDef.name.Clone() : new BoxedString($"{targetSlotName}'s {def.GetPreviewName()}");
                shopDesc = msgDef.shopDesc.Clone();
                sprite = msgDef.sprite.Clone();
            }
            else
            {
                name = string.IsNullOrEmpty(targetSlotName) ? new BoxedString(def.GetPreviewName()) : new BoxedString($"{targetSlotName}'s {def.GetPreviewName()}");
                shopDesc = new BoxedString(def.GetShopDesc());
                sprite = new EmptySprite();
            }
        }
    }
}
