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
        public string ServerUrl { get; set; }
        public int ServerPort { get; set; }
        public string SlotName { get; set; }
        public string ServerPassword { get; set; }
    }
}
