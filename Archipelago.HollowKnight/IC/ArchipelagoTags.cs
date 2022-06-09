using Archipelago.MultiClient.Net.Models;
using ItemChanger;
using ItemChanger.Tags;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Archipelago.MultiClient.Net.Enums;
using Archipelago.HollowKnight;

namespace Archipelago.HollowKnight.IC
{
    /// <summary>
    /// Tag attached to items that are involved with Archipelago.
    /// </summary>
    /// <remarks>
    /// ArchipelagoItemTags are attached to AP-randomized items to track what their location ID and player ("slot") are.  Additionally, they manage events
    /// for when items are picked up, ensuring that HK items for other players get replaced out and that all location checks actually get sent.
    /// </remarks>
    public class ArchipelagoItemTag : Tag
    {
        /// <summary>
        /// AP location ID for this item.
        /// </summary>
        public long Location { get; set; }
        /// <summary>
        /// AP player ID ("slot") for this item's recipient.
        /// </summary>
        public int Player { get; set; }
        /// <summary>
        /// Set 
        /// 
        /// </summary>
        public bool Hinted { get; set; } = false;

        public ArchipelagoItemTag() : base()
        {
        }

        public void ReadNetItem(NetworkItem networkItem)
        { 
            Location = networkItem.Location;
            Player = networkItem.Player;
        }

        public override void Load(object parent)
        {
            base.Load(parent);
            AbstractItem item = (AbstractItem)parent;
            // Archipelago.Instance.LogDebug($"In ArchipelagoItemTag:Load for {parent}");
            item.ModifyItem += ModifyItem;
            item.AfterGive += AfterGive;

            if(item.WasEverObtained())
            {
                Archipelago.Instance.CheckLocation(Location);
            }
        }

        private void AfterGive(ReadOnlyGiveEventArgs obj)
        {
            Archipelago.Instance.CheckLocation(Location);
        }

        private void ModifyItem(GiveEventArgs obj)
        {
            AbstractItem item = obj.Orig;
            if (item is not ArchipelagoItem)
            {
                // Item is for HK.  But is it ours?
                if (Player != Archipelago.Instance.Slot)
                {
                    // Create a dummy ArchipelagoItem and "give" the player that instead.
                    obj.Item = new ArchipelagoDummyItem(obj.Orig);
                }
            }
            // If checks are deferred, we're doing initial catchup -- don't report items we sent to other players.
            if (Archipelago.Instance.DeferringLocationChecks && Player != Archipelago.Instance.Slot) {
                var tags = obj.Item.GetTags<InteropTag>();
                foreach (var tag in tags)
                { 
                    if(tag.Message == "RecentItems")
                    {
                        tag.Properties["IgnoreItem"] = true;
                        return;
                    }
                }
                {
                    var tag = obj.Item.AddTag<InteropTag>();
                    tag.Message = "RecentItems";
                    tag.Properties["IgnoreItem"] = true;
                }
            }
        }

        public override void Unload(object parent)
        {
            ((AbstractItem)parent).ModifyItem -= ModifyItem;
            ((AbstractItem)parent).AfterGive -= AfterGive;
            base.Unload(parent);
        }
    }

    /// <summary>
    /// Tag attached to placements that are involved with Archipelago.
    /// </summary>
    /// <remarks>
    /// ArchipelagoPlacementTags are attached to placements containing AP-randomized.  
    /// They track whether the placement has been successfully hinted in AP (e.g. when previewed), and what its associated Location ID is.  This latter tracking facilitates
    /// a dictionary of location IDs to placements so when items are received from our own slot (e.g. same-slot coop or recovering a lost save) we can update the game
    /// world accordingly.
    /// </remarks>
    public class ArchipelagoPlacementTag : Tag
    {
        /// <summary>
        /// True if this location has been hinted AP, or is in the process of being hinted.
        /// </summary>
        public bool Hinted { get; set; }

        public override void Load(object parent)
        {
            base.Load(parent);
            AbstractPlacement pmt = (AbstractPlacement)parent;
            //Archipelago.Instance.LogDebug($"In ArchipelagoPlacementTag:Load for {parent}, locations ({String.Join(", ", PlacementUtils.GetLocationIDs(pmt))})");

            foreach (long locationId in PlacementUtils.GetLocationIDs(pmt))
            {
                Archipelago.Instance.placementsByLocationID[locationId] = pmt;
            }

            pmt.OnVisitStateChanged += OnVisitStateChanged;
            // If we've been previewed but never told AP that, tell it now
            if (!Hinted && pmt.Visited.HasFlag(VisitState.Previewed))
            {
                PlacementUtils.CreateLocationHint(pmt);
            }
        }

        public override void Unload(object parent)
        {
            //Archipelago.Instance.LogDebug($"In ArchipelagoPlacementTag:UNLOAD for {parent}, locations ({String.Join(", ", PlacementUtils.GetLocationIDs((AbstractPlacement)parent))})");
            ((AbstractPlacement)parent).OnVisitStateChanged -= OnVisitStateChanged;

            foreach (long locationId in PlacementUtils.GetLocationIDs((AbstractPlacement)parent))
            {
                Archipelago.Instance.placementsByLocationID.Remove(locationId);
            }

            base.Unload(parent);
        }

        private void OnVisitStateChanged(VisitStateChangedEventArgs obj)
        {
            if (!Hinted && obj.NewFlags.HasFlag(VisitState.Previewed))
            {
                // We are now previewed, but we weren't before.
                PlacementUtils.CreateLocationHint(obj.Placement);
            }
        }
    }
}
