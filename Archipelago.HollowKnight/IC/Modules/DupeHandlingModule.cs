using ItemChanger;
using ItemChanger.Items;
using ItemChanger.Modules;

namespace Archipelago.HollowKnight.IC.Modules;

public class DupeHandlingModule : Module
{
    public override void Initialize()
    {
        AbstractItem.ModifyRedundantItemGlobal += ModifyRedundantItem;
    }

    public override void Unload()
    {
        AbstractItem.ModifyRedundantItemGlobal -= ModifyRedundantItem;
    }

    private void ModifyRedundantItem(GiveEventArgs args)
    {
        args.Item = new SpawnLumafliesItem
        {
            name = $"Lumafly_Escape-{args.Orig.name}",
            UIDef = DupeUIDef.Of(args.Orig.UIDef)
        };
    }
}
