using Archipelago.MultiClient.Net.Enums;
using ItemChanger;

namespace Archipelago.HollowKnight.IC
{
    public class ArchipelagoDummyItem : AbstractItem
    {
        public ArchipelagoDummyItem()
        { }
        public ArchipelagoDummyItem(AbstractItem source)
        {
            this.name = source.name;
            this.UIDef = source.UIDef.Clone();
        }

        public override void GiveImmediate(GiveInfo info)
        {
            // Intentional no-op
        }
    }

    public class ArchipelagoItem : ArchipelagoDummyItem
    {
        public ArchipelagoItem(string name, string recipientName = null, ItemFlags itemFlags = 0) : base()
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
            //if(recipientName != null)
            //{
            //    name = $"{recipientName}'s {name}";
            //}
            this.name = name;
            UIDef = new ArchipelagoUIDef()
            {
                name = new BoxedString($"{recipientName}'s {name}"),
                shopDesc = new BoxedString(desc),
                sprite = new BoxedSprite(Archipelago.SmallSprite)
            };
        }
    }
}