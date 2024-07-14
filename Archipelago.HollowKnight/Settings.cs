using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Archipelago.HollowKnight
{
    public record ConnectionDetails
    {
        public string ServerUrl { get; set; } = "archipelago.gg";
        public int ServerPort { get; set; } = 38281;
        public string SlotName { get; set; }
        public string ServerPassword { get; set; }
    }

    public record APGlobalSettings
    {
        public ConnectionDetails MenuConnectionDetails { get; set; } = new();
    }

    public record APLocalSettings
    {
        public ConnectionDetails ConnectionDetails { get; set; }
        public int ItemIndex { get; set; }
        public string RoomSeed { get; set; }
        public long Seed { get; set; }
    }
}
