namespace Archipelago.HollowKnight
{
    public enum DeathLinkStatus
    {
        None = 0,
        Pending = 1,
        Dying = 2
    }

    public enum DeathLinkShadeHandling
    {
        Vanilla = 0,
        Shadeless = 1,
        Shade = 2
    }

    public enum WhitePalaceOption
    {
        Exclude = 0,
        KingFragment = 1,
        NoPathOfPain = 2,
        Include = 3
    }

    public enum EntranceRandoType
    {
        None = 0,
        MapArea = 1,
        FullArea = 2,
        Room = 3,
        ConnectedArea = 4,
        Doors = 5,
    }

    public enum ShuffleEntrancesMode
    {
        Coupled = 0,
        Decoupled = 1,
    }
}
