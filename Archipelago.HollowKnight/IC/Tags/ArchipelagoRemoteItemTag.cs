using Archipelago.HollowKnight.IC.Modules;
using Archipelago.MultiClient.Net.Models;
using ItemChanger;
using Newtonsoft.Json;
using System;

namespace Archipelago.HollowKnight.IC.Tags;


/// <summary>
/// Tag attached to items sent from other players
/// </summary>
public class ArchipelagoRemoteItemTag : Tag
{
    /// <summary>
    /// The slot ID of the sending player
    /// </summary>
    public int Sender { get; set; }

    /// <summary>
    /// The location ID in the sender's world
    /// </summary>
    public long LocationId { get; set; }

    /// <summary>
    /// The item ID
    /// </summary>
    public long ItemId { get; set; }

    [JsonConstructor]
    private ArchipelagoRemoteItemTag() { }

    public ArchipelagoRemoteItemTag(ItemInfo itemInfo)
    {
        if (itemInfo is ScoutedItemInfo)
        {
            throw new ArgumentException("Remote item tags should only be used on items received from other players and should not be initialized from scouts", nameof(itemInfo));
        }
        ArchipelagoMod.Instance.LogDebug($"Created remote tag for {itemInfo.ItemName} from {itemInfo.Player} at {itemInfo.LocationDisplayName}");
        Sender = itemInfo.Player;
        LocationId = itemInfo.LocationId;
        ItemId = itemInfo.ItemId;
    }

    public override void Load(object parent)
    {
        base.Load(parent);
        ArchipelagoRemoteItemCounterModule module = ItemChangerMod.Modules.GetOrAdd<ArchipelagoRemoteItemCounterModule>();
        module.IncrementSavedCountForItem(Sender, LocationId, ItemId);
    }
}
