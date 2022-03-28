using Archipelago.HollowKnight.IC;
using ItemChanger;

namespace Archipelago.HollowKnight.Grants
{
    internal class GrubGrants
    {
        private int grubCounter = 0;
        private AbstractPlacement[] grubs = new AbstractPlacement[46];

        public GrubGrants()
        {
            for (int i = 0; i < 46; i++)
            {
                var item = Finder.GetItem("Grub");
                var location = new ArchipelagoLocation($"Grub #{i}");
                var pmt = location.Wrap();
                pmt.Add(item);
                grubs[i] = pmt;
            }

            ItemChangerMod.AddPlacements(grubs);
        }

        public void GrantGrub()
        {
            if (grubCounter < grubs.Length)
            {
                var pmt = grubs[grubCounter++];
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
