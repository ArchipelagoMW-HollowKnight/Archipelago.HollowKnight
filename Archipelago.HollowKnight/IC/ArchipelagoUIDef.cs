using ItemChanger;
using ItemChanger.UIDefs;

namespace Archipelago.HollowKnight.IC
{
    internal class ArchipelagoUIDef : MsgUIDef
    {
        public static ArchipelagoUIDef CreateForReceivedItem(AbstractItem item, string sender)
        {
            return CreateForReceivedItem(item.GetResolvedUIDef(), sender);
        }

        public static ArchipelagoUIDef CreateForReceivedItem(UIDef source, string sender)
        {
            ArchipelagoUIDef result = new(source);
            result.name = new BoxedString($"{source.GetPostviewName()} from {sender}");
            return result;
        }
        public static ArchipelagoUIDef CreateForSentItem(AbstractItem item, string recipient)
        {
            return CreateForSentItem(item.UIDef, recipient);
        }

        public static ArchipelagoUIDef CreateForSentItem(UIDef source, string recipient)
        {
            ArchipelagoUIDef result = new ArchipelagoUIDef(source);
            result.name = new BoxedString($"{recipient}'s {source.GetPostviewName()}");
            return result;
        }

        internal ArchipelagoUIDef() : base()
        {
        }

        internal ArchipelagoUIDef(UIDef source) : base()
        {
            if (source is MsgUIDef msgDef)
            { 
                shopDesc = msgDef.shopDesc.Clone();
                sprite = msgDef.sprite.Clone();
            }
            else
            {
                shopDesc = new BoxedString(source.GetShopDesc());
                sprite = new EmptySprite();
            }
        }
    }
}
