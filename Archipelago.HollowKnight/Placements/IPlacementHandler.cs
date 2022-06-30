using ItemChanger;

namespace Archipelago.HollowKnight.Placements
{
    internal interface IPlacementHandler
    {
        bool CanHandlePlacement(string location);
        void HandlePlacement(AbstractPlacement pmt, AbstractItem item, string originalLocationName);
    }
}
