using System;
using System.Collections.Generic;
using System.Linq;
using Archipelago.HollowKnight.IC;
using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Models;
using ItemChanger;
using ItemChanger.Internal;
using ItemChanger.Placements;
using Steamworks;

namespace Archipelago.HollowKnight;

public static class HintTracker
{

    public static event Action OnArchipelagoHintUpdate;
    
    /// <summary>
    ///  List of MultiClient.Net Hint's
    /// </summary>
    public static List<Hint> Hints;

    private static ArchipelagoSession _session;

    private static void UpdateHints(Hint[] arrayHints)
    {
        Hints = arrayHints.ToList();
        foreach (Hint hint in Hints)
        {
            if (hint.FindingPlayer != Archipelago.Instance.session.ConnectionInfo.Slot)
                continue;

            if (!Archipelago.Instance.placementsByLocationID.ContainsKey(hint.LocationId)) continue;
            AbstractPlacement placement = Archipelago.Instance.placementsByLocationID[hint.LocationId];

            if (placement == null)
                continue;
            
            // set the hinted tag for the single item in the placement that was hinted for.
            foreach (ArchipelagoItemTag tag in placement.Items.Select(item => item.GetTag<ArchipelagoItemTag>()).Where(tag => tag.Location == hint.LocationId))
            {
                tag.Hinted = true;
            }
            
            // if all items inside a placement have been hinted for then mark the entire placement as hinted.
            if (placement.Items.TrueForAll(item => item.GetTag<ArchipelagoItemTag>().Hinted))
            {
                placement.GetTag<ArchipelagoPlacementTag>().Hinted = true;
            }

            if (placement is ShopPlacement)
            {
                List<(string, AbstractItem)> previewText = new();
                ShopPlacement shop = (ShopPlacement) placement;
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
                shop.OnPreviewBatch(previewText);
            }
            else
            {
                List<string> previewText = new();
                foreach (AbstractItem item in placement.Items)
                {
                    previewText.Add(item.GetTag<ArchipelagoItemTag>().Hinted
                        ? item.GetPreviewWithCost()
                        : Language.Language.Get("???", "IC"));
                }
                
                placement.OnPreview(string.Join(", ", previewText));
            }
            
        }
        
        try
        {
            OnArchipelagoHintUpdate?.Invoke();
        }
        catch (Exception ex)
        {
            Archipelago.Instance.LogError($"Error invoking OnArchipelagoHintUpdate:\n {ex}");
        }
    }

    public static void Start(ArchipelagoSession archipelagoSession)
    {
        _session = archipelagoSession;
        _session.DataStorage.TrackHints(UpdateHints);
    }
}