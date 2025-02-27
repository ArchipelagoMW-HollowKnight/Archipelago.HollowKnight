﻿using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Exceptions;
using Archipelago.MultiClient.Net.Models;
using ItemChanger;
using ItemChanger.Internal;
using ItemChanger.Modules;
using ItemChanger.Tags;
using MenuChanger;
using Modding;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
        public static GiveInfo RemoteGiveInfo = new()
        {
            FlingType = FlingType.DirectDeposit,
            Callback = null,
            Container = Container.Unknown,
            MessageType = MessageType.Corner
        };

        /// <summary>
        /// A preset GiveInfo structure that avoids creating geo and outputs no messages, e.g. for Start Items.
        /// </summary>
        public static GiveInfo SilentGiveInfo = new()
        {
            FlingType = FlingType.DirectDeposit,
            Callback = null,
            Container = Container.Unknown,
            MessageType = MessageType.None
        };

        private ArchipelagoSession session => ArchipelagoMod.Instance.session;

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
            session.Locations.CheckedLocationsUpdated -= OnLocationChecksUpdated;
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
                ArchipelagoMod.Instance.LogWarn("SendLocationsAsync disconnected");
                deferredLocationChecks.AddRange(locationIds);
                ReportDisconnect();
            }
            catch (Exception ex)
            {
                ArchipelagoMod.Instance.LogError("Unexpected error in SendLocationsAsync");
                ArchipelagoMod.Instance.LogError(ex);
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

            ArchipelagoMod.Instance.LogDebug($"Marking location {locationId} as checked.");
            if (!ArchipelagoPlacementTag.PlacementsByLocationId.TryGetValue(locationId, out pmt))
            {
                ArchipelagoMod.Instance.LogDebug($"Could not find a placement for location {locationId}");
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
                    HeroController.instance.AddGeo(ArchipelagoMod.Instance.SlotData.Options.StartingGeo);
                    hasEverRecievedStartingGeo = true;
                }
                readyToSendReceiveChecks = true;
                await Synchronize();
                session.Locations.CheckedLocationsUpdated += OnLocationChecksUpdated;
            }
        }

        private void OnLocationChecksUpdated(ReadOnlyCollection<long> newCheckedLocations)
        {
            foreach (long location in newCheckedLocations)
            {
                ThreadSupport.BeginInvoke(() => MarkLocationAsChecked(location, false));
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
            for (int i = 0; i < ArchipelagoMod.Instance.LS.ItemIndex; i++)
            {
                ItemInfo seen = session.Items.DequeueItem();
                ArchipelagoMod.Instance.LogDebug($"Fast-forwarding past already-obtained {seen.ItemName} at index {i}");
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

            APLocalSettings settings = ArchipelagoMod.Instance.LS;

            ItemInfo itemInfo = session.Items.DequeueItem(); // Read the next item
            if (settings.ItemIndex >= session.Items.Index) // We've already handled this, so be done
            {
                return true;
            }
            ArchipelagoMod.Instance.LogDebug($"Item Index from lib is: {session.Items.Index}. From APSettings it is: {settings.ItemIndex}");

            try
            {
                ReceiveItem(itemInfo, silentGive);
            }
            catch (Exception ex)
            {
                ArchipelagoMod.Instance.LogError($"Unexpected exception during receive for item {JsonConvert.SerializeObject(itemInfo.ToSerializable())}: {ex}");
            }
            finally
            {
                ArchipelagoMod.Instance.LS.ItemIndex++;
            }

            return true;
        }

        private void ReceiveItem(ItemInfo itemInfo, bool silentGive)
        {
            string name = itemInfo.ItemName;
            ArchipelagoMod.Instance.LogDebug($"Receiving item {itemInfo.ItemId} with name {name}. " +
                $"Slot is {itemInfo.Player}. Location is {itemInfo.LocationId} with name {itemInfo.LocationName}");

            if (itemInfo.Player == ArchipelagoMod.Instance.session.Players.ActivePlayer.Slot && itemInfo.LocationId > 0)
            {
                MarkLocationAsChecked(itemInfo.LocationId, silentGive);
                return;
            }

            // If we're still here, this is an item from someone else.  We'll make up our own dummy placement and grant the item.
            AbstractItem item = Finder.GetItem(name);
            if (item == null)
            {
                ArchipelagoMod.Instance.LogDebug($"Could not find an item named '{name}'. " +
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
