using ItemChanger.Modules;
using Modding;
using System.Collections.Generic;

namespace Archipelago.HollowKnight.IC.Modules
{
    public class RepositionShadeModule : Module
    {
        private static readonly Dictionary<string, (float x, float y)> ShadeSpawnPositionFixes = new()
        {
            { "Abyss_08", (90.0f, 90.0f) },  // Lifeblood Core room.  Even outside of deathlink, shades spawn out of bounds.
            { "Room_Colosseum_Spectate", (124.0f, 10.0f) },  // Shade spawns inside inaccessible arena
            { "Resting_Grounds_09", (7.4f, 10.0f) },  // Shade spawns underground.
            { "Runes1_18", (11.5f, 23.0f) },  // Shade potentially spawns on the wrong side of an inaccessible gate.
        };

        public override void Initialize()
        {
            ModHooks.AfterPlayerDeadHook += FixUnreachableShadePosition;
        }

        public override void Unload()
        {
            ModHooks.AfterPlayerDeadHook -= FixUnreachableShadePosition;
        }
        private void FixUnreachableShadePosition()
        {
            // Fixes up some bad shade placements by vanilla HK.
            PlayerData pd = PlayerData.instance;
            if (ShadeSpawnPositionFixes.TryGetValue(pd.shadeScene, out (float x, float y) position))
            {
                pd.shadePositionX = position.x;
                pd.shadePositionY = position.y;
            }
        }
    }
}
