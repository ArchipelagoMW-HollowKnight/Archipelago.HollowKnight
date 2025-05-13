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
    
    public enum DeathLinkOverride
    {
        UseYaml = 0,
        OverrideOn = 1,
        OverrideOff = 2
    }

    public enum WhitePalaceOption
    {
        Exclude = 0,
        KingFragment = 1,
        NoPathOfPain = 2,
        Include = 3
    }
}
