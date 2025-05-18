using GlobalEnums;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.Collections.Generic;
using System.IO;

namespace Archipelago.HollowKnight.IC.RM;
public record StartDef
{
    public static Dictionary<string, StartDef> Lookup
    {
        get
        {
            if (field != null)
            {
                return field;
            }
            JsonSerializer ser = new()
            {
                TypeNameHandling = TypeNameHandling.Auto,
                Converters =
                {
                    new StringEnumConverter()
                }
            };
            using StreamReader r = new(typeof(StartDef).Assembly.GetManifestResourceStream("Archipelago.HollowKnight.Resources.Data.starts.json"));
            using JsonTextReader reader = new(r);
            return field = ser.Deserialize<Dictionary<string, StartDef>>(reader);
        }
    }

    public string Name { get; init; }
    public string SceneName { get; init; }
    public float X { get; init; }
    public float Y { get; init; }
    public MapZone Zone { get; init; }
    /// <summary>
    /// Granted transition in logic
    /// </summary>
    public string Transition { get; init; }

    public ItemChanger.StartDef ToItemChangerStartDef()
    {
        return new ItemChanger.StartDef
        {
            SceneName = SceneName,
            X = X,
            Y = Y,
            MapZone = (int)Zone,
            RespawnFacingRight = true,
            SpecialEffects = ItemChanger.SpecialStartEffects.Default | ItemChanger.SpecialStartEffects.SlowSoulRefill,
        };
    }
}
