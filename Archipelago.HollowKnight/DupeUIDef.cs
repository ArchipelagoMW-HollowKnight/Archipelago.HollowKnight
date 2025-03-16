using ItemChanger;
using ItemChanger.UIDefs;
using UnityEngine;

namespace Archipelago.HollowKnight;

public class DupeUIDef : MsgUIDef
{
    public static MsgUIDef Of(UIDef inner)
    {
        if (inner is MsgUIDef msg)
        {
            return new SplitUIDef
            {
                preview = new BoxedString(msg.GetPreviewName()),
                name = new BoxedString($"Nothing ({msg.GetPostviewName()})"),
                shopDesc = msg.shopDesc?.Clone(),
                sprite = msg.sprite?.Clone(),
            };
        }
        return new DupeUIDef(inner);
    }

    public UIDef Inner { get; set; }
    private DupeUIDef(UIDef inner)
    {
        Inner = inner;
        sprite = new ItemChangerSprite("ShopIcons.LampBug");
        if (inner is null)
        {
            name = new BoxedString("Nothing (Dupe)");
            shopDesc = new BoxedString("");
        }
        else
        {
            // with good practice these should never be accessed but better not to break stuff
            name = new BoxedString($"Nothing ({inner.GetPostviewName()})");
            shopDesc = new BoxedString(inner.GetShopDesc());
        }
    }

    public override Sprite GetSprite() => Inner is not null ? Inner.GetSprite() : base.GetSprite();
    public override string GetPreviewName() => Inner is not null ? Inner.GetPreviewName() : base.GetPreviewName();
    public override string GetPostviewName() => Inner is not null ? Inner.GetPostviewName() : base.GetPostviewName();
    public override string GetShopDesc() => Inner is not null ? Inner.GetShopDesc() : base.GetShopDesc();
}
