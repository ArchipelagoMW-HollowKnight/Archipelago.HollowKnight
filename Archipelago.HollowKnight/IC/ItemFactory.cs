using Archipelago.MultiClient.Net.Models;
using ItemChanger;
using System;

namespace Archipelago.HollowKnight.IC
{
    internal class ItemFactory
    {
        public AbstractItem CreateMyItem(string itemName, NetworkItem netItem)
        {
            AbstractItem item = Finder.GetItem(itemName);
            if (item == null)
            {
                Archipelago.Instance.LogError($"Could not find local item with name {itemName}");
                throw new NullReferenceException($"Could not find local item with name {itemName}");
            }

            AddArchipelagoTag(item, netItem);
            return item;
        }

        public AbstractItem CreateRemoteItem(string slotName, string itemName, NetworkItem netItem)
        {
            AbstractItem item = Finder.GetItem(itemName);
            if (item != null)
            {
                // this is a remote HK item - make it a no-op, but cosmetically correct
                item = new ArchipelagoDummyItem(item);
                item.UIDef = ArchipelagoUIDef.CreateForSentItem(item, slotName);
            }
            else
            {
                // Items from other games
                item = new ArchipelagoItem(itemName, slotName, netItem.Flags);
            }

            AddArchipelagoTag(item, netItem);
            return item;
        }

        private void AddArchipelagoTag(AbstractItem item, NetworkItem netItem)
        {
            ArchipelagoItemTag itemTag = item.AddTag<ArchipelagoItemTag>();
            itemTag.ReadNetItem(netItem);
        }
    }
}
