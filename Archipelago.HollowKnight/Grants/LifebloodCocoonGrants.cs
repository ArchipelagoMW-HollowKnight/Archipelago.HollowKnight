using System;
using System.Linq;
using Archipelago.HollowKnight.IC;
using ItemChanger;
using ItemChanger.Items;

namespace Archipelago.HollowKnight.Grants
{
    internal class LifebloodCocoonGrants
    {
        private int smallCocoonCounter = 0;
        private int largeCocoonCounter = 0;

        private AbstractPlacement[] smallCocoons = new AbstractPlacement[10];
        private AbstractPlacement[] largeCocoons = new AbstractPlacement[10];

        public LifebloodCocoonGrants()
        {
            for (int i = 0; i < 10; i++)
            {
                var item = new LifebloodItem() { amount = 2};
                var loc = new ArchipelagoLocation($"Two Blue Masks #{i + 1}");
                var pmt = loc.Wrap();
                pmt.Add(item);
                smallCocoons[i] = pmt;
            }

            for (int i = 0; i < 10; i++)
            {
                var item = new LifebloodItem() { amount = 3 };
                var loc = new ArchipelagoLocation($"Three Blue Masks #{i + 1}");
                var pmt = loc.Wrap();
                pmt.Add(item);
                largeCocoons[i] = pmt;
            }

            ItemChangerMod.AddPlacements(smallCocoons.Concat(largeCocoons));
        }

        public static bool IsLifebloodCocoonItem(string itemName)
        {
            return itemName.StartsWith("Lifeblood_Cocoon");
        }

        public void GrantSmallCocoon()
        {
            if (smallCocoonCounter < smallCocoons.Length)
            {
                var pmt = smallCocoons[smallCocoonCounter++];
                pmt.GiveAll(new GiveInfo()
                {
                    Container = Container.Unknown,
                    FlingType = FlingType.DirectDeposit,
                    MessageType = MessageType.Any,
                });
            }
        }

        public void GrantLargeCocoon()
        {
            if (largeCocoonCounter < largeCocoons.Length)
            {
                var pmt = largeCocoons[largeCocoonCounter++];
                pmt.GiveAll(new GiveInfo()
                {
                    Container = Container.Unknown,
                    FlingType = FlingType.DirectDeposit,
                    MessageType = MessageType.Any,
                });
            }
        }

        public void GrantCocoonByName(string itemName)
        {
            Archipelago.Instance.LogDebug($"Granting lifeblood item by name: {itemName}");
            switch (itemName)
            {
                case "Lifeblood_Cocoon_Small":
                    GrantSmallCocoon();
                    break;
                case "Lifeblood_Cocoon_Large":
                    GrantLargeCocoon();
                    break;
            }
        }
    }
}
