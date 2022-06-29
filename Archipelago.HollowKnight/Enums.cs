using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Archipelago.HollowKnight
{
    public enum DeathLinkStatus
    {
        None = 0,
        Pending = 1,
        Dying = 2
    }

    public enum DeathLinkType
    {
        None = 0,
        Shadeless = 1,
        Vanilla = 2,
        Shade = 3
    }

    public enum WhitePalaceOption
    {
        Exclude = 0,
        KingFragment = 1,
        NoPathOfPain = 2,
        Include = 3
    }
}
