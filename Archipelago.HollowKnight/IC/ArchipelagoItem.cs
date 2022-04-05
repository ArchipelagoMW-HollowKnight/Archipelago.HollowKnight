using Archipelago.MultiClient.Net.Enums;
using ItemChanger;
using ItemChanger.Tags;
using ItemChanger.UIDefs;

namespace Archipelago.HollowKnight.IC
{
    public class ArchipelagoItem : AbstractItem
    {
        public ArchipelagoItem(string name, string targetSlotName = null, ItemFlags itemFlags = 0)
        {
            string desc;
            if (itemFlags.HasFlag(ItemFlags.Advancement))
            {
                desc = "This otherworldly artifact looks very important. Somebody probably really needs it.";
            }
            else if (itemFlags.HasFlag(ItemFlags.NeverExclude))
            {
                desc = "This otherworldly artifact looks like it might be useful to someone.";
            }
            else
            {
                desc = "I'm not entirely sure what this is. It appears to be a strange artifact from another world.";
            }
            if (itemFlags.HasFlag(ItemFlags.Trap))
            {
                desc += " Seems kinda suspicious though. It might be full of bees.";
            }
            this.name = name;
            if(targetSlotName != null)
            {
                name = $"{targetSlotName}'s {name}";
            }
            UIDef = new ArchipelagoUIDef(new MsgUIDef
            {
                name = new BoxedString(name),
                shopDesc = new BoxedString(desc),
                sprite = new BoxedSprite(Archipelago.SmallSprite)
            });
        }

        // INFO: this can be used to restore placements from save
        protected override void OnLoad()
        {
            base.OnLoad();
        }

        protected override void OnUnload()
        {
            base.OnUnload();
        }

        public override void GiveImmediate(GiveInfo info)
        {
            
        }
    }
}