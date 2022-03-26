using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ItemChanger;
using ItemChanger.Tags;

namespace Archipelago.HollowKnight
{
    internal class DisguisedVoidItem : AbstractItem
    {
        public DisguisedVoidItem(AbstractItem originalItem)
        {
            name = originalItem.name;
            UIDef = new ItemChanger.UIDefs.MsgUIDef
            {
                name = new BoxedString(originalItem.GetPreviewName()),
                shopDesc = new BoxedString(originalItem.UIDef.GetShopDesc()),
                // TODO: Copying sprite from originalItem doesn't seem to work. Crashes Unity upon trying to load sprite from ItemChanger.
                sprite = new EmptySprite(),
            };

            InteropTag tag = AddTag<InteropTag>();
            tag.Message = "RecentItems";
            //tag.Properties["DisplayMessage"] = $"{this.name}";
        }

        public override void GiveImmediate(GiveInfo info)
        {
            ItemChanger.Internal.MessageController.Enqueue(UIDef.GetSprite(), name);
        }
    }
}
