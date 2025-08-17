using Archipelago.MultiClient.Net.Models;
using ItemChanger.Modules;
using System.Collections.Generic;

namespace Archipelago.HollowKnight.IC.Modules;

public class ArchipelagoRemoteItemCounterModule : Module
{
    /// <summary>
    /// Full history of remote items received and saved by the client. Keys are player, location, item to finally reach a count.
    /// </summary>
    private readonly Dictionary<int, Dictionary<long, Dictionary<long, int>>> savedItemCounts = new();

    /// <summary>
    /// History of items seen when receiving from server. Keys are player, location, item to finally reach a count.
    /// </summary>

    private readonly Dictionary<int, Dictionary<long, Dictionary<long, int>>> serverSeenItemCounts = new();

    public override void Initialize()
    {
    }

    public override void Unload()
    {
    }

    /// <summary>
    /// Determines whether the specified item should be received from the server based on the current counts. Should be called before receiving the item.
    /// </summary>
    /// <param name="item">The item to evaluate for receiving from the server.</param>
    /// <returns>
    /// <see langword="true"/> if receiving the item would result in the server count exceeding the local saved count;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    public bool ShouldReceiveServerItem(ItemInfo item)
    {
        int currentSavedCount = EnsureCountExists(item.Player, item.LocationId, item.ItemId, savedItemCounts);
        int currentServerCount = EnsureCountExists(item.Player, item.LocationId, item.ItemId, serverSeenItemCounts);

        // if obtaining this item will have sent more items from the server than we have locally, we should receive the item. otherwise we should skip it.
        return currentServerCount + 1 > currentSavedCount;
    }

    /// <summary>
    /// Increments the server-side count for the specified item.
    /// </summary>
    /// <param name="item">The <see cref="ItemInfo"/> object representing the item whose count is to be incremented.  This includes details
    /// such as the player, location, and item identifier.</param>
    public void IncrementServerCountForItem(ItemInfo item)
    {
        EnsureCountExists(item.Player, item.LocationId, item.ItemId, serverSeenItemCounts);
        IncrementCurrentCountForItem(item.Player, item.LocationId, item.ItemId, serverSeenItemCounts);
    }

    /// <summary>
    /// Increments the saved count for a specific item at a given location for a specified player.
    /// </summary>
    /// <param name="player">The identifier of the player for whom the item's saved count is being incremented.</param>
    /// <param name="locationId">The identifier of the location where the item is stored.</param>
    /// <param name="itemId">The identifier of the item whose saved count is being incremented.</param>
    public void IncrementSavedCountForItem(int player, long locationId, long itemId)
    {
        EnsureCountExists(player, locationId, itemId, savedItemCounts);
        IncrementCurrentCountForItem(player, locationId, itemId, savedItemCounts);
    }

    private static void IncrementCurrentCountForItem(int player, long locationId, long itemId, Dictionary<int, Dictionary<long, Dictionary<long, int>>> itemCounts, int incrementBy = 1)
    {
        itemCounts[player][locationId][itemId] += incrementBy;
    }

    private static int EnsureCountExists(int player, long locationId, long itemId, Dictionary<int, Dictionary<long, Dictionary<long, int>>> itemCounts)
    {
        if (!itemCounts.TryGetValue(player, out Dictionary<long, Dictionary<long, int>> a))
        {
            itemCounts[player] = a = new();
        }

        if (!a.TryGetValue(locationId, out Dictionary<long, int> b))
        {
            a[locationId] = b = new();
        }

        if (!b.TryGetValue(itemId, out int count))
        {
            b[itemId] = count = 0;
        }
        return count;
    }
}
