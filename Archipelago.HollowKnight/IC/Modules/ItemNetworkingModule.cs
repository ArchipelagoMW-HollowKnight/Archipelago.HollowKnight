using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Exceptions;
using Archipelago.MultiClient.Net.Models;
using ItemChanger;
using ItemChanger.Internal;
using ItemChanger.Modules;
using ItemChanger.Tags;
using Modding;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Archipelago.HollowKnight.IC.Modules
{
    /// <summary>
    /// Handles the sending and receiving of items from the server
    /// </summary>
    public class ItemNetworkingModule : Module
    {
        /// <summary>
        /// A preset GiveInfo structure that avoids creating geo and places messages in the corner.
        /// </summary>
        private static GiveInfo RemoteGiveInfo = new()
        {
            FlingType = FlingType.DirectDeposit,
            Callback = null,
            Container = Container.Unknown,
            MessageType = MessageType.Corner
        };

        /// <summary>
        /// A preset GiveInfo structure that avoids creating geo and outputs no messages, e.g. for Start Items.
        /// </summary>
        private static GiveInfo SilentGiveInfo = new()
        {
            FlingType = FlingType.DirectDeposit,
            Callback = null,
            Container = Container.Unknown,
            MessageType = MessageType.None
        };

        private ArchipelagoSession session => Archipelago.Instance.session;

        private bool networkErrored;
        private bool readyToSendReceiveChecks;

        [JsonProperty]
        private List<long> deferredLocationChecks = [];
        [JsonProperty]
        private bool hasEverRecievedStartingGeo = false;

        public override void Initialize()
        {
            networkErrored = false;
            readyToSendReceiveChecks = false;
            ModHooks.HeroUpdateHook += PollForItems;
            On.GameManager.FinishedEnteringScene += DoInitialSyncAndStartSendReceive;
        }

        public override void Unload()
        {
            // DoInitialSyncAndStartSendReceive unsubscribes itself
            ModHooks.HeroUpdateHook -= PollForItems;
        }

        public async Task SendLocationsAsync(params long[] locationIds)
        {
            if (!readyToSendReceiveChecks)
            {
                deferredLocationChecks.AddRange(locationIds);
                return;
            }

            if (networkErrored)
            {
                deferredLocationChecks.AddRange(locationIds);
                ReportDisconnect();
                return;
            }

            try
            {
                await Task.Run(() => session.Locations.CompleteLocationChecks(locationIds)).TimeoutAfter(1000);
            }
            catch (Exception ex) when (ex is TimeoutException or ArchipelagoSocketClosedException)
            {
                Archipelago.Instance.LogWarn("SendLocationsAsync disconnected");
                deferredLocationChecks.AddRange(locationIds);
                ReportDisconnect();
            }
            catch (Exception ex)
            {
                Archipelago.Instance.LogError("Unexpected error in SendLocationsAsync");
                Archipelago.Instance.LogError(ex);
                deferredLocationChecks.AddRange(locationIds);
                ReportDisconnect();
            }
        }

        public void MarkLocationAsChecked(long locationId, bool silentGive)
        {
            // Called when marking a location as checked remotely (i.e. through ReceiveItem, etc.)
            // This also grants items at said locations.
            AbstractPlacement pmt;
            bool hadNewlyObtainedItems = false;
            bool hadUnobtainedItems = false;

            Archipelago.Instance.LogDebug($"Marking location {locationId} as checked.");
            if (!ArchipelagoPlacementTag.PlacementsByLocationId.TryGetValue(locationId, out pmt))
            {
                Archipelago.Instance.LogDebug($"Could not find a placement for location {locationId}");
                return;
            }

            foreach (AbstractItem item in pmt.Items)
            {
                if (!item.GetTag(out ArchipelagoItemTag tag))
                {
                    hadUnobtainedItems = true;
                    continue;
                }

                if (item.WasEverObtained())
                {
                    continue;
                }

                if (tag.Location != locationId)
                {
                    hadUnobtainedItems = true;
                    continue;
                }

                hadNewlyObtainedItems = true;
                pmt.AddVisitFlag(VisitState.ObtainedAnyItem);

                GiveInfo giveInfo = silentGive ? SilentGiveInfo : RemoteGiveInfo;
                item.Give(pmt, giveInfo.Clone());
            }

            if (hadNewlyObtainedItems && !hadUnobtainedItems)
            {
                pmt.AddVisitFlag(VisitState.Opened | VisitState.Dropped | VisitState.Accepted |
                                 VisitState.ObtainedAnyItem);
            }
        }

        private async void DoInitialSyncAndStartSendReceive(On.GameManager.orig_FinishedEnteringScene orig, GameManager self)
        {
            orig(self);
            if (!readyToSendReceiveChecks)
            {
                On.GameManager.FinishedEnteringScene -= DoInitialSyncAndStartSendReceive;
                if (!hasEverRecievedStartingGeo)
                {
                    HeroController.instance.AddGeo(Archipelago.Instance.SlotOptions.StartingGeo);
                    hasEverRecievedStartingGeo = true;
                }
                readyToSendReceiveChecks = true;
                await Synchronize();
            }
        }

        private void PollForItems()
        {
            if (!readyToSendReceiveChecks || !session.Items.Any())
            {
                return;
            }

            ReceiveNextItem(false);
        }

        private async Task Synchronize()
        {
            // discard any items that we have already handled from previous sessions
            for (int i = 0; i < Archipelago.Instance.LS.ItemIndex; i++)
            {
                ItemInfo seen = session.Items.DequeueItem();
                Archipelago.Instance.LogDebug($"Fast-forwarding past already-obtained {seen.ItemName} at index {i}");
            }
            // receive from the server any items that are pending
            while (ReceiveNextItem(true)) { }
            // ensure any already-checked locations (co-op, restarting save) are marked cleared
            foreach (long location in session.Locations.AllLocationsChecked)
            {
                MarkLocationAsChecked(location, true);
            }
            // send out any pending items that didn't get to the network from the previous session
            long[] pendingLocations = deferredLocationChecks.ToArray();
            deferredLocationChecks.Clear();
            await SendLocationsAsync(pendingLocations);
        }

        private bool ReceiveNextItem(bool silentGive)
        {
            if (!session.Items.Any())
            {
                return false; // No items are waiting.
            }

            APLocalSettings settings = Archipelago.Instance.LS;

            ItemInfo itemInfo = session.Items.DequeueItem(); // Read the next item
            if (settings.ItemIndex >= session.Items.Index) // We've already handled this, so be done
            {
                return true;
            }
            Archipelago.Instance.LogDebug($"Item Index from lib is: {session.Items.Index}. From APSettings it is: {settings.ItemIndex}");

            try
            {
                ReceiveItem(itemInfo, silentGive);
            }
            catch (Exception ex)
            {
                Archipelago.Instance.LogError($"Unexpected exception during receive for item {JsonConvert.SerializeObject(itemInfo.ToSerializable())}: {ex}");
            }
            finally
            {
                Archipelago.Instance.LS.ItemIndex++;
            }

            return true;
        }

        private void ReceiveItem(ItemInfo itemInfo, bool silentGive)
        {
            string name = itemInfo.ItemName;
            Archipelago.Instance.LogDebug($"Receiving item {itemInfo.ItemId} with name {name}. " +
                $"Slot is {itemInfo.Player}. Location is {itemInfo.LocationId} with name {itemInfo.LocationName}");

            if (itemInfo.Player == Archipelago.Instance.Slot && itemInfo.LocationId > 0)
            {
                MarkLocationAsChecked(itemInfo.LocationId, silentGive);
                return;
            }

            // If we're still here, this is an item from someone else.  We'll make up our own dummy placement and grant the item.
            AbstractItem item = Finder.GetItem(name);
            if (item == null)
            {
                Archipelago.Instance.LogDebug($"Could not find an item named '{name}'. " +
                    $"This means that item {itemInfo.ItemId} was not received.");
                return;
            }

            string sender;
            if (itemInfo.LocationId == -1)
            {
                sender = "Cheat Console";
            }
            else if (itemInfo.LocationId == -2)
            {
                sender = "Start";
            }
            else if (itemInfo.Player == 0)
            {
                sender = "Archipelago";
            }
            else
            {
                sender = session.Players.GetPlayerName(itemInfo.Player);
            }
            InteropTag recentItemsTag = item.AddTag<InteropTag>();
            recentItemsTag.Message = "RecentItems";
            recentItemsTag.Properties["DisplaySource"] = sender;

            RemotePlacement pmt = RemotePlacement.GetOrAddSingleton();
            item.Load();
            pmt.Add(item);

            GiveInfo giveInfo = silentGive ? SilentGiveInfo : RemoteGiveInfo;
            item.Give(pmt, giveInfo.Clone());
        }

        public void ReportDisconnect()
        {
            networkErrored = true;
            MessageController.Enqueue(
                null,
                "Error: Lost connection to Archipelago server"
            );
            MessageController.Enqueue(
                null,
                "Reload your save to attempt to reconnect."
            );
        }
    }
}
