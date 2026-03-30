using ItemChanger;
using ItemChanger.Modules;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Archipelago.HollowKnight.IC.Modules;

public class RepickableFlowerModule : Module
{
    public override void Initialize()
    {
        Events.AddSceneChangeEdit(SceneNames.Room_Mansion, SpawnFlower);
    }

    public override void Unload()
    {
        Events.RemoveSceneChangeEdit(SceneNames.Room_Mansion, SpawnFlower);
    }

    private void SpawnFlower(Scene scene)
    {
        GameObject flowerSource = ArchipelagoMod.Instance.Preloads.GetNewFlowerPickup();
        flowerSource.transform.position = new Vector3(25.0f, 6.4f, 2f);
        flowerSource.SetActive(true);
    }
}
