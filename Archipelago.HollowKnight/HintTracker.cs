using System;
using System.Collections.Generic;
using System.Linq;
using Archipelago.HollowKnight.IC;
using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Models;
using ItemChanger;
using ItemChanger.Internal;

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
        foreach (var hint in Hints)
        {
            if (hint.FindingPlayer != Archipelago.Instance.session.ConnectionInfo.Slot)
                continue;

            var locationName = StripShopSuffix(_session.Locations.GetLocationNameFromId(hint.LocationId));
            var location = Finder.GetLocation(locationName);
            if (location == null)
                continue;
            if (!Ref.Settings.Placements.ContainsKey(locationName))
                continue;

            var placement = Ref.Settings.Placements[locationName];

            if (placement == null)
                continue;
            
            //get all items in the placement and set the hinted attribute in ArchipelagoItemTag to true.
            foreach (var tag in placement.Items.Select(item => item.GetTag<ArchipelagoItemTag>()).Where(tag => tag.Location == hint.LocationId))
            {
                tag.Hinted = true;
            }
            
            //if there is only 1 item in this placement mark the whole placement as visited.
            if (placement.Items.Count > 1) continue;
            placement.GetTag<ArchipelagoPlacementTag>().Hinted = true;
            placement.AddVisitFlag(VisitState.Previewed);
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
    
    private static string StripShopSuffix(string location)
    {
        if (string.IsNullOrEmpty(location))
        {
            return null;
        }

        var names = new[]
        {
            LocationNames.Sly_Key, LocationNames.Sly, LocationNames.Iselda, LocationNames.Salubra,
            LocationNames.Leg_Eater, LocationNames.Egg_Shop, LocationNames.Seer, LocationNames.Grubfather
        };

        foreach (var name in names)
        {
            if (location.StartsWith(name))
            {
                return location.Substring(0, name.Length);
            }
        }

        return location;
    }

    public static void Start(ArchipelagoSession archipelagoSession)
    {
        _session = archipelagoSession;
        _session.DataStorage.TrackHints(UpdateHints);
    }
}