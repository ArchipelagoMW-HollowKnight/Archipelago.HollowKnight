using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using ItemChanger;

namespace Archipelago.HollowKnight
{
    internal class StackableItemGrants
    {
        private int idolCounter = 0;
        private int journalCounter = 0;
        private int sealCounter = 0;
        private int arcaneCounter = 0;
        private int rancidCounter = 0;

        private AbstractPlacement[] kingsIdols = new AbstractPlacement[8];
        private AbstractPlacement[] wanderersJournals = new AbstractPlacement[14];
        private AbstractPlacement[] hallownestSeals = new AbstractPlacement[17];
        private AbstractPlacement[] arcaneEggs = new AbstractPlacement[4];
        private AbstractPlacement[] rancidEggs = new AbstractPlacement[21];

        public StackableItemGrants()
        {
            for (int i = 0; i < 8; i++)
            {
                var item = Finder.GetItem("King's_Idol");
                var location = new ArchipelagoLocation("King's Idol");
                var pmt = location.Wrap();
                pmt.Add(item);
                kingsIdols[i] = pmt;
            }

            for (int i = 0; i < 14; i++)
            {
                var item = Finder.GetItem("Wanderer's_Journal");
                var location = new ArchipelagoLocation("Wanderer's Journal");
                var pmt = location.Wrap();
                pmt.Add(item);
                wanderersJournals[i] = pmt;
            }

            for (int i = 0; i < 17; i++)
            {
                var item = Finder.GetItem("Hallownest_Seal");
                var location = new ArchipelagoLocation("Hallownest Seal");
                var pmt = location.Wrap();
                pmt.Add(item);
                hallownestSeals[i] = pmt;
            }

            for (int i = 0; i < 4; i++)
            {
                var item = Finder.GetItem("Arcane_Egg");
                var location = new ArchipelagoLocation("Arcane Egg");
                var pmt = location.Wrap();
                pmt.Add(item);
                arcaneEggs[i] = pmt;
            }

            for (int i = 0; i < 21; i++)
            {
                var item = Finder.GetItem("Rancid_Egg");
                var location = new ArchipelagoLocation("Rancid Egg");
                var pmt = location.Wrap();
                pmt.Add(item);
                rancidEggs[i] = pmt;
            }

            ItemChangerMod.AddPlacements(kingsIdols.Concat(wanderersJournals).Concat(hallownestSeals).Concat(arcaneEggs).Concat(rancidEggs));
        }

        public static bool IsStackableItem(string itemName)
        {
            return new string[] { "King's_Idol", "Wanderer's_Journal", "Hallownest_Seal", "Arcane_Egg", "Rancid_Egg" }.Contains(itemName);
        }

        public void GrantItemByName(string itemName)
        {
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
    }
}
