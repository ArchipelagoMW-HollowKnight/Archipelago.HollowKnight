using Archipelago.Gifting.Net.Service;
using Archipelago.Gifting.Net.Traits;
using Archipelago.Gifting.Net.Versioning.Gifts.Current;
using Archipelago.MultiClient.Net;
using ItemChanger;
using ItemChanger.Modules;
using ItemChanger.Tags;
using MenuChanger;
using System.Collections.Generic;
using System.Linq;

namespace Archipelago.HollowKnight.IC.Modules;

public class GiftingModule : Module
{
    private static readonly string[] AcceptedTraits = [GiftFlag.Mana, GiftFlag.Life, "Artifact"];

    private ArchipelagoSession session => ArchipelagoMod.Instance.session;
    private GiftingService giftingService;

    public override void Initialize()
    {
        if (ArchipelagoMod.Instance.GS.EnableGifting)
        {
            giftingService = new GiftingService(session);
            On.GameManager.FinishedEnteringScene += BeginGifting;
        }
    }

    public override void Unload()
    {
        if (giftingService != null)
        {
            giftingService.CloseGiftBox();
            giftingService.OnNewGift -= ReceiveOrRefundGift;
            giftingService = null;
        }
    }

    private async void BeginGifting(On.GameManager.orig_FinishedEnteringScene orig, GameManager self)
    {
        orig(self);
        On.GameManager.FinishedEnteringScene -= BeginGifting;
        giftingService.OnNewGift += ReceiveOrRefundGift;
        Dictionary<string, Gift> pendingGifts = await giftingService.CheckGiftBoxAsync();
        foreach (Gift gift in pendingGifts.Values)
        {
            ReceiveOrRefundGift(gift);
        }
        giftingService.OpenGiftBox(false, AcceptedTraits);
    }

    private void ReceiveOrRefundGift(Gift gift)
    {
        giftingService.RemoveGiftFromGiftBox(gift.ID);
        GiftTrait bestTrait = PickBestMatchingTrait(gift);
        if (bestTrait != null)
        {
            string itemName;
            if (bestTrait.Trait == GiftFlag.Mana)
            {
                // soul refill based on quality. Average is 90 (large totems), below is 54, above is 200, scaling linearly
                if (bestTrait.Quality >= 2.2)
                {
                    itemName = ItemNames.Soul_Totem_Path_of_Pain;
                }
                else if (bestTrait.Quality <= 0.6)
                {
                    itemName = ItemNames.Soul_Totem_B;
                }
                else
                {
                    itemName = ItemNames.Soul_Totem_A;
                }
            }
            else if (bestTrait.Trait == GiftFlag.Life)
            {
                // blue hearts based on quality. Average is 2 blue masks, scaling linearly from that
                // todo - do we want XL/XS lifeblood for more interesting variance?
                if (bestTrait.Quality >= 1.5)
                {
                    itemName = ItemNames.Lifeblood_Cocoon_Large;
                }
                else
                {
                    itemName = ItemNames.Lifeblood_Cocoon_Small;
                }
            }
            else if (bestTrait.Trait == "Artifact")
            {
                // relic based on quality (average case is Hallownest seal, scales roughly linearly from that)
                if (bestTrait.Quality <= 0.4)
                {
                    itemName = ItemNames.Wanderers_Journal;
                }
                else if (bestTrait.Quality >= 2.7)
                {
                    itemName = ItemNames.Arcane_Egg;
                }
                else if (bestTrait.Quality >= 1.8)
                {
                    itemName = ItemNames.Kings_Idol;
                }
                else
                {
                    itemName = ItemNames.Hallownest_Seal;
                }
            }
            else
            {
                // safety net in case we update acceptedtraits
                ArchipelagoMod.Instance.LogWarn($"Got an unexpected trait {bestTrait} for gift {gift}");
                giftingService.RefundGift(gift);
                return;
            }

            string sender = session.Players.GetPlayerName(gift.SenderSlot);
            DispatchItem(itemName, gift.Amount, sender);
        }
        else
        {
            giftingService.RefundGift(gift);
        }
    }

    private GiftTrait PickBestMatchingTrait(Gift gift)
    {
        GiftTrait best = null;
        foreach (GiftTrait trait in gift.Traits)
        {
            if (AcceptedTraits.Contains(trait.Trait))
            {
                if (best == null || trait.Quality > best.Quality)
                {
                    best = trait;
                }
            }
        }
        return best;
    }

    private void DispatchItem(string itemName, int amount, string sender)
    {
        ThreadSupport.BeginInvoke(() =>
        {
            for (int i = 0; i < amount; i++)
            {
                AbstractItem item = Finder.GetItem(itemName);
                InteropTag recentItemsTag = item.AddTag<InteropTag>();
                recentItemsTag.Message = "RecentItems";
                recentItemsTag.Properties["DisplaySource"] = sender;

                item.Load();
                item.Give(null, ItemNetworkingModule.RemoteGiveInfo);
                item.Unload();
            }
        });
    }
}
