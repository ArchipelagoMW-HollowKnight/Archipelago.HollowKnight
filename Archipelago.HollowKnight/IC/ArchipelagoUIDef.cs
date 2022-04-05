using ItemChanger;
using ItemChanger.UIDefs;

namespace Archipelago.HollowKnight.IC
{
    internal class ArchipelagoUIDef : MsgUIDef
    {
        public ArchipelagoUIDef(UIDef def)
        {
            if (def is MsgUIDef msgDef)
            {
                name = msgDef.name.Clone();
                shopDesc = msgDef.shopDesc.Clone();
                sprite = msgDef.sprite.Clone();
            }
            else
            {
                name = new BoxedString(def.GetPreviewName());
                shopDesc = new BoxedString(def.GetShopDesc());
                sprite = new EmptySprite();
            }
        }
    }
}
