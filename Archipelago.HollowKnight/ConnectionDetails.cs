using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Archipelago.HollowKnight
{
    public record ConnectionDetails
    {
        public string ServerUrl { get; set; } = "localhost";
        public int ServerPort { get; set; } = 38281;
        public string SlotName { get; set; } = "WhoAmI1";
        public string ServerPassword { get; set; }
    }
}
