using ItemChanger;
using ItemChanger.UIDefs;

namespace Archipelago.HollowKnight.IC
{
    internal class ArchipelagoUIDef : MsgUIDef
    {
        public static string GetSentItemName(AbstractItem item)
        {
            return item.name switch
            {
                ItemNames.Grub => "A grub!",
                ItemNames.Grimmkin_Flame => "Grimmkin Flame",
                ItemNames.Rancid_Egg => "Rancid Egg",
                _ => item.UIDef.GetPostviewName(),
            };
        }

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
            ArchipelagoUIDef result = new(item.UIDef);
            result.name = new BoxedString($"{recipient}'s {GetSentItemName(item)}");
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
