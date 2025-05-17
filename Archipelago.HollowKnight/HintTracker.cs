using Archipelago.HollowKnight.IC;
using Archipelago.HollowKnight.IC.Modules;
using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.Exceptions;
using Archipelago.MultiClient.Net.Models;
using ItemChanger;
using ItemChanger.Modules;
using ItemChanger.Placements;
using ItemChanger.Tags;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine.SceneManagement;

namespace Archipelago.HollowKnight;

public class HintTracker : Module
{

    public static event Action OnArchipelagoHintUpdate;
    
    /// <summary>
    ///  List of MultiClient.Net Hint's
    /// </summary>
    public static List<Hint> Hints;
    /// <summary>
    /// List of placement hints to send on scene change or when closing out the session
    /// </summary>
    private List<AbstractPlacement> PendingPlacementHints;

    private ArchipelagoSession session;

    private void UpdateHints(Hint[] arrayHints)
    {
        Hints = arrayHints.ToList();
        foreach (Hint hint in Hints)
        {
            if (hint.FindingPlayer != ArchipelagoMod.Instance.session.ConnectionInfo.Slot)
            {
                continue;
            }

            if (!ArchipelagoPlacementTag.PlacementsByLocationId.ContainsKey(hint.LocationId))
            {
                continue;
            }

            AbstractPlacement placement = ArchipelagoPlacementTag.PlacementsByLocationId[hint.LocationId];

            if (placement == null)
            {
                continue;
            }

            // set the hinted tag for the single item in the placement that was hinted for.
            foreach (ArchipelagoItemTag tag in placement.Items.Select(item => item.GetTag<ArchipelagoItemTag>())
                         .Where(tag => tag.Location == hint.LocationId))
            {
                tag.Hinted = true;
            }

            // if all items inside a placement have been hinted for then mark the entire placement as hinted.
            if (placement.Items.TrueForAll(item => item.GetTag<ArchipelagoItemTag>().Hinted))
            {
                placement.GetTag<ArchipelagoPlacementTag>().Hinted = true;
            }

            if (placement is ShopPlacement shop)
            {
                List<(string, AbstractItem)> previewText = new();
                foreach (AbstractItem item in shop.Items)
                {
                    if (item.GetTag<ArchipelagoItemTag>().Hinted)
                    {
                        previewText.Add((item.GetPreviewWithCost(), item));
                    }
                    else
                    {
                        previewText.Add((Language.Language.Get("???", "IC"), item));
                    }

                }
                MultiPreviewRecordTag previewRecordTag = shop.GetOrAddTag<MultiPreviewRecordTag>();
                previewRecordTag.previewTexts ??= new string[shop.Items.Count];
                
                foreach ((string, AbstractItem item) p in previewText)
                {
                    string str = p.Item1;
                    int index = shop.Items.IndexOf(p.item);
                    if (index >= 0)
                    {
                        previewRecordTag.previewTexts[index] = str;
                    }
                }
            }
            else
            {
                List<string> previewText = new();
                foreach (AbstractItem item in placement.Items)
                {
                    if(item.WasEverObtained())
                    {
                        continue;
                    }

                    previewText.Add(item.GetTag<ArchipelagoItemTag>().Hinted
                        ? item.GetPreviewWithCost()
                        : Language.Language.Get("???", "IC"));
                }

                placement.GetOrAddTag<PreviewRecordTag>().previewText = string.Join(Language.Language.Get("COMMA_SPACE", "IC"), previewText);
            }

        }

        try
        {
            OnArchipelagoHintUpdate?.Invoke();
        }
        catch (Exception ex)
        {
            ArchipelagoMod.Instance.LogError($"Error invoking OnArchipelagoHintUpdate:\n {ex}");
        }
    }

    public override void Initialize()
    {
        PendingPlacementHints = [];

        session = ArchipelagoMod.Instance.session;

        // do most setup in OnEnterGame so save data can completely load, we need to
        // populate all the AP placements to sync with server
        Events.OnEnterGame += OnEnterGame;
    }

    private void OnEnterGame()
    {
        session.DataStorage.TrackHints(UpdateHints);

        AbstractItem.AfterGiveGlobal += UpdateHintFoundStatus;
        Events.OnSceneChange += SendHintsOnSceneChange;
    }

    public override async void Unload()
    {
        Events.OnEnterGame -= OnEnterGame;
        AbstractItem.AfterGiveGlobal -= UpdateHintFoundStatus;
        Events.OnSceneChange -= SendHintsOnSceneChange;
        await SendPlacementHintsAsync();
    }

    public void HintPlacement(AbstractPlacement pmt)
    {
        // todo - accommodate different hinting times (immediate/never)
        PendingPlacementHints.Add(pmt);
    }

    private void UpdateHintFoundStatus(ReadOnlyGiveEventArgs args)
    {
        if (Hints != null && args.Orig.GetTag(out ArchipelagoItemTag tag))
        {
            long location = tag.Location;
            foreach (Hint hint in Hints)
            {
                if (hint.LocationId == location)
                {
                    hint.Found = true; 
                    try
                    {
                        OnArchipelagoHintUpdate?.Invoke();
                    }
                    catch (Exception ex)
                    {
                        ArchipelagoMod.Instance.LogError($"Error invoking OnArchipelagoHintUpdate:\n {ex}");
                    }
                    break;
                }
            }
        }
    }

    private async void SendHintsOnSceneChange(Scene scene)
    {
        await SendPlacementHintsAsync();
    }

    private async Task SendPlacementHintsAsync()
    {
        if (!PendingPlacementHints.Any())
        {
            return;
        }

        HashSet<ArchipelagoItemTag> hintedTags = new();
        HashSet<long> hintedLocationIDs = new();
        ArchipelagoItemTag tag;

        foreach (AbstractPlacement pmt in PendingPlacementHints)
        {
            foreach (AbstractItem item in pmt.Items)
            {
                if (item.GetTag(out tag) && !tag.Hinted)
                {
                    if ((tag.Flags.HasFlag(ItemFlags.Advancement) || tag.Flags.HasFlag(ItemFlags.NeverExclude))
                        && !item.WasEverObtained()
                        && !item.HasTag<DisableItemPreviewTag>())
                    {
                        hintedTags.Add(tag);
                        hintedLocationIDs.Add(tag.Location);
                    }
                    else
                    {
                        tag.Hinted = true;
                    }
                }
            }
        }

        PendingPlacementHints.Clear();
        if (!hintedLocationIDs.Any())
        {
            return;
        }

        ArchipelagoMod.Instance.LogDebug($"Hinting {hintedLocationIDs.Count()} locations.");
        try
        {
            await session.Locations.ScoutLocationsAsync(true, hintedLocationIDs.ToArray())
                .ContinueWith(x =>
                {
                    bool result = !x.IsFaulted;
                    foreach (ArchipelagoItemTag tag in hintedTags)
                    {
                        tag.Hinted = result;
                    }
                }).TimeoutAfter(1000);
        }
        catch (Exception ex) when (ex is ArchipelagoSocketClosedException or TimeoutException)
        {
            ItemChangerMod.Modules.Get<ItemNetworkingModule>().ReportDisconnect();
        }
    }
}