using System.Linq;
using Archipelago.HollowKnight.IC;
using ItemChanger;

namespace Archipelago.HollowKnight.Grants
{
    internal class StackableItemGrants
    {
        private int idolCounter = 0;
        private int journalCounter = 0;
        private int sealCounter = 0;
        private int arcaneCounter = 0;
        private int rancidCounter = 0;
        private int simpleKeyCounter = 0;
        private int charmNotchCounter = 0;

        private AbstractPlacement[] kingsIdols = new AbstractPlacement[8];
        private AbstractPlacement[] wanderersJournals = new AbstractPlacement[14];
        private AbstractPlacement[] hallownestSeals = new AbstractPlacement[17];
        private AbstractPlacement[] arcaneEggs = new AbstractPlacement[4];
        private AbstractPlacement[] rancidEggs = new AbstractPlacement[21];
        private AbstractPlacement[] simpleKeys = new AbstractPlacement[4];
        //TODO: Possibly make it an option to put notches in starting inventory and default to 0;
        private AbstractPlacement[] charmNotches = new AbstractPlacement[8];

        public StackableItemGrants()
        {
            for (int i = 0; i < 8; i++)
            {
                var item = Finder.GetItem("King's_Idol");
                var location = new ArchipelagoLocation($"King's Idol  #{i + 1}");
                var pmt = location.Wrap();
                pmt.Add(item);
                kingsIdols[i] = pmt;
            }

            for (int i = 0; i < 14; i++)
            {
                var item = Finder.GetItem("Wanderer's_Journal");
                var location = new ArchipelagoLocation($"Wanderer's Journal  #{i + 1}");
                var pmt = location.Wrap();
                pmt.Add(item);
                wanderersJournals[i] = pmt;
            }

            for (int i = 0; i < 17; i++)
            {
                var item = Finder.GetItem("Hallownest_Seal");
                var location = new ArchipelagoLocation($"Hallownest Seal #{i + 1}");
                var pmt = location.Wrap();
                pmt.Add(item);
                hallownestSeals[i] = pmt;
            }

            for (int i = 0; i < 4; i++)
            {
                var item = Finder.GetItem("Arcane_Egg");
                var location = new ArchipelagoLocation($"Arcane Egg #{i + 1}");
                var pmt = location.Wrap();
                pmt.Add(item);
                arcaneEggs[i] = pmt;
            }

            for (int i = 0; i < 21; i++)
            {
                var item = Finder.GetItem("Rancid_Egg");
                var location = new ArchipelagoLocation($"Rancid Egg #{i + 1}");
                var pmt = location.Wrap();
                pmt.Add(item);
                rancidEggs[i] = pmt;
            }

            for (int i = 0; i < 4; i++)
            {
                var item = Finder.GetItem("Simple_Key");
                var location = new ArchipelagoLocation($"Simple Key #{i + 1}");
                var pmt = location.Wrap();
                pmt.Add(item);
                simpleKeys[i] = pmt;
            }

            for (int i = 0; i < 8; i++)
            {
                var item = Finder.GetItem("Charm_Notch");
                var location = new ArchipelagoLocation($"Charm Notch #{i + 1}");
                var pmt = location.Wrap();
                pmt.Add(item);
                charmNotches[i] = pmt;
            }

            ItemChangerMod.AddPlacements(kingsIdols
                                        .Concat(wanderersJournals)
                                        .Concat(hallownestSeals)
                                        .Concat(arcaneEggs)
                                        .Concat(rancidEggs)
                                        .Concat(simpleKeys)
                                        .Concat(charmNotches));
        }

        public static bool IsStackableItem(string itemName)
        {
            return new string[] 
            { 
                "King's_Idol", "Wanderer's_Journal", "Hallownest_Seal", 
                "Arcane_Egg", "Rancid_Egg", "Simple_Key", "Charm_Notch" 
            }.Contains(itemName);
        }

        public void GrantItemByName(string itemName)
        {
            Archipelago.Instance.LogDebug($"Granting stackable item by name: {itemName}");
            switch (itemName)
            {
                case "King's_Idol":
                    GrantKingsIdol();
                    break;
                case "Wanderer's_Journal":
                    GrantWanderersJournal();
                    break;
                case "Hallownest_Seal":
                    GrantHallownestSeal();
                    break;
                case "Arcane_Egg":
                    GrantArcaneEgg();
                    break;
                case "Rancid_Egg":
                    GrantRancidEgg();
                    break;
                case "Simple_Key":
                    GrantSimpleKey();
                    break;
                case "Charm_Notch":
                    GrantCharmNotch();
                    break;
            }
        }

        public void GrantKingsIdol()
        {
            if (idolCounter < kingsIdols.Length)
            {
                var pmt = kingsIdols[idolCounter++];
                pmt.GiveAll(new GiveInfo()
                {
                    Container = Container.Unknown,
                    FlingType = FlingType.DirectDeposit,
                    MessageType = MessageType.Corner,
                });
            }
        }

        public void GrantWanderersJournal()
        {
            if (journalCounter < wanderersJournals.Length)
            {
                var pmt = wanderersJournals[journalCounter++];
                pmt.GiveAll(new GiveInfo()
                {
                    Container = Container.Unknown,
                    FlingType = FlingType.DirectDeposit,
                    MessageType = MessageType.Corner,
                });
            }
        }

        public void GrantHallownestSeal()
        {
            if (sealCounter < hallownestSeals.Length)
            {
                var pmt = hallownestSeals[sealCounter++];
                pmt.GiveAll(new GiveInfo()
                {
                    Container = Container.Unknown,
                    FlingType = FlingType.DirectDeposit,
                    MessageType = MessageType.Corner,
                });
            }
        }

        public void GrantArcaneEgg()
        {
            if (arcaneCounter < arcaneEggs.Length)
            {
                var pmt = arcaneEggs[arcaneCounter++];
                pmt.GiveAll(new GiveInfo()
                {
                    Container = Container.Unknown,
                    FlingType = FlingType.DirectDeposit,
                    MessageType = MessageType.Corner,
                });
            }
        }

        public void GrantRancidEgg()
        {
            if (rancidCounter < rancidEggs.Length)
            {
                var pmt = rancidEggs[rancidCounter++];
                pmt.GiveAll(new GiveInfo()
                {
                    Container = Container.Unknown,
                    FlingType = FlingType.DirectDeposit,
                    MessageType = MessageType.Corner,
                });
            }
        }

        public void GrantSimpleKey()
        {
            if (simpleKeyCounter < simpleKeys.Length)
            {
                var pmt = simpleKeys[simpleKeyCounter++];
                pmt.GiveAll(new GiveInfo()
                {
                    Container = Container.Unknown,
                    FlingType = FlingType.DirectDeposit,
                    MessageType = MessageType.Corner,
                });
            }
        }

        public void GrantCharmNotch()
        {
            if (charmNotchCounter < charmNotches.Length)
            {
                var pmt = charmNotches[charmNotchCounter++];
                pmt.GiveAll(new GiveInfo()
                {
                    Container = Container.Unknown,
                    FlingType = FlingType.DirectDeposit,
                    MessageType = MessageType.Corner,
                });
            }
        }
    }
}
