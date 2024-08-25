using System;
using System.Collections.Generic;

namespace Archipelago.HollowKnight
{
    public static class DeathLinkMessages
    {
        public static readonly List<string> DefaultMessages = new()
        {
            "@ died.",
            "@ has perished.",
            "@ made poor life choices.",
            "@ didn't listen to Hornet's advice.",
            "@ took damage equal to or more than their current HP.",
            "@ made a fatal mistake.",
            "@ threw some shade at @.",
            "@ decided to set up a Shade Skip.", // A System of Vibrant Colors (edited)
            "Hopefully @ didn't have a fragile charm equipped.", // Koatlus
            "A true servant gives all for the Kingdom.  Let @ relieve you of your life.", // Koatlus
            "Through @'s sacrifice, you are now dead.", // Koatlus
            "The truce remains.  Our vigil holds.  @ must respawn.", // Koatlus
            "Hopefully @ didn't have a fragile charm equipped.", // Koatlus
        };

        public static readonly List<string> UnknownMessages = new()
        {
            "@ has died in a manner most unusual.",
            "@ found a way to break the game, and the game broke @ back.",
            "@ has lost The Game",
        };

        public static readonly Dictionary<int, List<string>> MessagesByType = new()
        {
            {
                1, // Deaths from enemy damage 
                new List<string>
                {
                    "@ has discovered that there are bugs in Hallownest.",
                    "@ should have dodged.",
                    "@ should have jumped.",
                    "@ significantly mistimed their parry attempt.",
                    "@ should have considered equipping Dreamshield.",
                    "@ must have never fought that enemy before.",
                    "@ did not make it to phase 2.",
                    "@ dashed in the wrong direction.", // Murphmario
                    "@ tried to talk it out.", // SnowOfAllTrades
                    "@ made masterful use of their vulnerability frames.",
                }
            },
            {
                2, // Deaths from spikes
                new List<string>
                {
                    "@ was in the wrong place.",
                    "@ mistimed their jump.",
                    "@ didn't see the sharp things.",
                    "@ didn't see that saw.",
                    "@ fought the spikes and the spikes won.",
                    "@ sought roses but found only thorns.",
                    "@ was pricked to death.", // A System of Vibrant Colors
                    "@ dashed in the wrong direction.", // Murphmario
                    "@ found their own Path of Pain.", // Fatman
                    "@ has strayed from the White King's roads.", // Koatlus
                }
            },
            {
                3, // Deaths from acid
                new List<string>
                {
                    "@ was in the wrong place.",
                    "@ mistimed their jump.",
                    "@ forgot their floaties.",
                    "What @ thought was H2O was H2SO4.",
                    "@ wishes they could swim.",
                    "@ used the wrong kind of dive.",
                    "@ got into a fight with a pool of liquid and lost.",
                    "@ forgot how to swim", // squidy
                }
            },
            {
                999, // Deaths in the dream realm
                new List<string>
                {
                    "@ dozed off for good.",
                    "@ was caught sleeping on the job.",
                    "@ sought dreams but found only nightmares.",
                    "@ got lost in Limbo.",
                    "Good night, @.",
                    "@ is resting in pieces.",
                    "@ exploded into a thousand pieces of essence.",
                    "Hey, @, you're finally awake.",
                }
            },
        };

        private static readonly Random random = new(); // This is only messaging, so does not need to be seeded.

        public static string GetDeathMessage(int cause, string player)
        {
            // Build candidate death messages.
            List<string> messages;
            bool knownCauseOfDeath = MessagesByType.TryGetValue(cause, out messages);

            if (knownCauseOfDeath)
            {
                messages = new(messages);
                messages.AddRange(DefaultMessages);
            }
            else
            {
                messages = UnknownMessages;
            }

            // Choose one at random
            string message = messages[random.Next(0, messages.Count)].Replace("@", player);

            // If it's an unknown death, tag in some debugging info
            if (!knownCauseOfDeath)
            {
                ArchipelagoMod.Instance.LogWarn($"UNKNOWN cause of death {cause}");
                message += $" (Type: {cause})";
            }

            return message;
        }
    };
}