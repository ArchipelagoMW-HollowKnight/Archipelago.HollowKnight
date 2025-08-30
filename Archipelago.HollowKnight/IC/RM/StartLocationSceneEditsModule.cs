using Archipelago.HollowKnight.SlotDataModel;
using ItemChanger;
using ItemChanger.Extensions;
using ItemChanger.Modules;
using UnityEngine;
using UnityEngine.SceneManagement;
using UObject = UnityEngine.Object;

namespace Archipelago.HollowKnight.IC.RM;
public class StartLocationSceneEditsModule : Module
{
    public override void Initialize()
    {
        ToggleSceneHooks(true);
    }

    public override void Unload()
    {
        ToggleSceneHooks(false);
    }

    private static void ToggleSceneHooks(bool toggle)
    {
        SlotOptions options = ArchipelagoMod.Instance.SlotData.Options;
        string startLocation = options.StartLocationName ?? StartLocationNames.Kings_Pass;

        switch (startLocation)
        {
            case "Ancestral Mound":
                if (options.RandomizeNail)
                {
                    if (toggle)
                    {
                        Events.AddSceneChangeEdit(SceneNames.Crossroads_ShamanTemple, DestroyPlanksForAncestralMoundStart);
                    }
                    else
                    {
                        Events.RemoveSceneChangeEdit(SceneNames.Crossroads_ShamanTemple, DestroyPlanksForAncestralMoundStart);
                    }
                }
                break;

            case "Fungal Core":
                if (toggle)
                {
                    Events.AddSceneChangeEdit(SceneNames.Fungus2_30, CreateBounceShroomsForFungalCoreStart);
                }
                else
                {
                    Events.RemoveSceneChangeEdit(SceneNames.Fungus2_30, CreateBounceShroomsForFungalCoreStart);
                }

                break;

            case "West Crossroads":
                if (toggle)
                {
                    Events.AddSceneChangeEdit(SceneNames.Crossroads_36, MoveShadeMarkerForWestCrossroadsStart);
                }
                else
                {
                    Events.RemoveSceneChangeEdit(SceneNames.Crossroads_36, MoveShadeMarkerForWestCrossroadsStart);
                }

                break;
        }


    }

    // Destroy planks in cursed nail mode because we can't slash them
    private static void DestroyPlanksForAncestralMoundStart(Scene to)
    {
        foreach ((_, GameObject go) in to.Traverse())
        {
            if (go.name.StartsWith("Plank"))
            {
                UObject.Destroy(go);
            }
        }
    }

    private static void CreateBounceShroomsForFungalCoreStart(Scene to)
    {
        GameObject bounceShroom = to.FindGameObjectByName("Bounce Shroom C");

        GameObject s0 = UObject.Instantiate(bounceShroom);
        s0.transform.SetPosition3D(12.5f, 26f, 0f);
        s0.SetActive(true);

        GameObject s1 = UObject.Instantiate(bounceShroom);
        s1.transform.SetPosition3D(12.5f, 54f, 0f);
        s1.SetActive(true);

        GameObject s2 = UObject.Instantiate(bounceShroom);
        s2.transform.SetPosition3D(21.7f, 133f, 0f);
        s2.SetActive(true);
    }

    private static void MoveShadeMarkerForWestCrossroadsStart(Scene to)
    {
        GameObject marker = to.FindGameObject("_Props/Hollow_Shade Marker 1");
        marker.transform.position = new(46.2f, 28f);
    }
}
