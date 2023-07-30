using ItemChanger;

namespace Archipelago.HollowKnight;

public static class Extensions
{
    public static string GetPreviewWithCost(this AbstractItem item)
    {
        string text = item.GetPreviewName();
        if (item.GetTag(out CostTag tag))
        {
            text += "  -  " + tag.Cost.GetCostText();
        }
        return text;
    }
}