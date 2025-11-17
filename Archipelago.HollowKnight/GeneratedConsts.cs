using DataDrivenConstants.Marker;

namespace Archipelago.HollowKnight;

[JsonData("$.*~", "**/Data/starts.json")]
[ReplacementRule("'", "")]
public static partial class StartLocationNames { }
