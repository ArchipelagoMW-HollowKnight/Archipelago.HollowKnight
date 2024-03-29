﻿using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Models;
using ItemChanger;
using ItemChanger.Items;
using ItemChanger.Tags;
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

        public AbstractItem CreateRemoteItem(AbstractPlacement targetPlacement, string slotName, string itemName, NetworkItem netItem)
        {
            ArchipelagoSession session = Archipelago.Instance.session;
            string game = session.Players.Players[session.ConnectionInfo.Team][netItem.Player].Game;

            AbstractItem orig = Finder.GetItem(itemName);
            AbstractItem item;
            if (game == "Hollow Knight" && orig != null)
            {
                // this is a remote HK item - make it a no-op, but cosmetically correct
                item = new ArchipelagoDummyItem(orig);
                item.UIDef = ArchipelagoUIDef.CreateForSentItem(orig, slotName);

                // give the placement the correct cosmetic soul totem or geo rock type if appropriate
                if (orig is SoulTotemItem totem)
                {
                    targetPlacement.GetOrAddTag<SoulTotemSubtypeTag>().Type = totem.soulTotemSubtype;
                }
                else if (orig is GeoRockItem rock)
                {
                    targetPlacement.GetOrAddTag<GeoRockSubtypeTag>().Type = rock.geoRockSubtype;
                }
            }
            else
            {
                // Items from other games, or an unknown HK item
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
