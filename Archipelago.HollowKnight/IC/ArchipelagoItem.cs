using Archipelago.MultiClient.Net.Enums;
using ItemChanger;

namespace Archipelago.HollowKnight.IC
{
    public class ArchipelagoDummyItem : AbstractItem
    {
        public string PreferredContainerType { get; set; } = Container.Unknown;

        public override string GetPreferredContainer() => PreferredContainerType;

        public ArchipelagoDummyItem()
        { }
        public ArchipelagoDummyItem(AbstractItem source)
        {
            this.name = source.name;
            this.UIDef = source.UIDef.Clone();
            PreferredContainerType = source.GetPreferredContainer();
        }

        public override bool GiveEarly(string containerType)
        {
            // any container (e.g. a grub or soul totem) that would not normally fling a shiny
            // in vanilla should not go out of its way to do so for this
            return containerType switch
            {
                Container.Unknown
                or Container.Shiny
                or Container.Chest
                  => false,
                _ => true
            };
        }

        public override void GiveImmediate(GiveInfo info)
        {
            // Intentional no-op
        }
    }

    public class ArchipelagoItem : AbstractItem
    {
        public ArchipelagoItem(string name, string recipientName = null, ItemFlags itemFlags = 0)
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
            UIDef = new ArchipelagoUIDef()
            {
                name = new BoxedString($"{recipientName}'s {name}"),
                shopDesc = new BoxedString(desc),
                sprite = new ArchipelagoSprite { key = "IconSmall" }
            };
        }

        public override void GiveImmediate(GiveInfo info)
        {
            // intentional no-op
        }
    }
}
