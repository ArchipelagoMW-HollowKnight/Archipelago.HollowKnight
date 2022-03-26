using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ItemChanger;
using ItemChanger.Items;
using UnityEngine;

namespace Archipelago.HollowKnight.Grants
{
    internal static class LifebloodCocoonGrants
    {
        public static bool IsLifebloodCocoonItem(string itemName)
        {
            return itemName.StartsWith("Lifeblood_Cocoon");
        }

        public static void AwardBlueHeartsFromItemsSafely(params AbstractItem[] items)
        {
            var totalAmountToAdd = 0;
            foreach (AbstractItem item in items)
            {
                var lifebloodItem = item as LifebloodItem;
                if (lifebloodItem != null)
                {
                    totalAmountToAdd += lifebloodItem.amount;
                }
            }

            var currentBlue = PlayerData.instance.GetInt("healthBlue");
            var currentMax = PlayerData.instance.GetInt("maxHealth");

            var sum = currentMax + currentBlue;

            currentBlue += totalAmountToAdd;

            PlayerData.instance.SetInt("healthBlue", currentBlue);

            //TODO: Get blue health display working.
            //var bhpDisplayFsmOriginal = PlayMakerFSM.FsmList.FirstOrDefault(x => x != null && x.FsmName == "blue_health_display");
            //var bhpControlFsm = PlayMakerFSM.FsmList.FirstOrDefault(x => x != null && x.FsmName == "Blue Health Control");
            //var bhpDisplayFsm = GameObject.Instantiate(bhpDisplayFsmOriginal, bhpControlFsm.transform);
            //if (bhpDisplayFsm != null)
            //{
            //    Archipelago.Instance.LogDebug("bhpDisplayFsm was NOT null.");
            //    var startIdleBool = bhpDisplayFsm.FsmVariables.FindFsmBool("Start Idle");
            //    startIdleBool.SafeAssign(false);

            //    var bhpNumInt = bhpDisplayFsm.FsmVariables.FindFsmInt("Health Number");
            //    bhpNumInt.SafeAssign(currentBlue);

            //    //bhpDisplayFsm.SetState("Init");
            //    //bhpDisplayFsm.SendEvent("APPEAR");
            //    Archipelago.Instance.LogDebug("Done with bhpDisplayFsm.");
            //}
            //else
            //{
            //    Archipelago.Instance.LogDebug("bhpDisplayFsm was null.");
            //}
        }
    }
}
