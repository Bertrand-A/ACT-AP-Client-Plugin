using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;
using System.Threading.Tasks;
using UnityEngine;

namespace ACTAP
{
    [HarmonyPatch(typeof(NPC_Prawnathan), "Awake")]
    class InvisPrawnathanFix
    {
        [HarmonyPostfix]
        static void Postfix(NPC_Prawnathan __instance)
        {
            __instance.transform.SetParent(null);
            __instance.gameObject.SetActive(true);
        }
    }

    [HarmonyPatch(typeof(NPC_Prawnathan), "SeenOtherShops")]
    class PrawnathanSeenOtherShopsFix
    {
        [HarmonyPrefix]
        static bool Prefix(ref bool __result)
        {
            __result = true;
            return false;
        }
    }

    [HarmonyPatch(typeof(NPC_Prawnathan), "Interact")]
    class PrawnathanInteractFix
    {
        [HarmonyPrefix]
        static bool Prefix(NPC_Prawnathan __instance)
        {
            bool seenGarbage = CrabFile.current.progressData[ProgressData.NewCarciniaProgress.SeenGarbageDayScene].unlocked;
            bool visitedAll = CrabFile.current.progressData[ProgressData.NewCarciniaProgress.VisitedAllShops].unlocked;

            Debug.Log($"SeenGarbageDayScene: {seenGarbage}, VisitedAllShops: {visitedAll}");
            
            if (__instance.nonShopkeepVariant && !visitedAll)
            {
                return true;
            }

            __instance.nonShopkeepVariant = false;

            Vector3 vector3 = Util.ZeroY(Player.singlePlayer.transform.position - __instance.transform.position);
            if (__instance.facePlayerOnInteract)
                __instance.lookVector = vector3;
            Player.singlePlayer.SnapLook(-vector3);

            // If we've seen the Trash Day cutscene, keep the shop open
            if (seenGarbage)
            {
                System.Collections.IEnumerator shopRoutine = (System.Collections.IEnumerator)AccessTools.Method(typeof(NPC_Prawnathan), "OpenShopRoutine").Invoke(__instance, null);
                __instance.StartCoroutine(shopRoutine);
            }
            // If we've visited all shops but haven't seen trash day yet, trigger it
            else if (visitedAll)
            {
                System.Collections.IEnumerator trashRoutine = (System.Collections.IEnumerator)AccessTools.Method(typeof(NPC_Prawnathan), "TrashDayRoutine").Invoke(__instance, null);
                __instance.StartCoroutine(trashRoutine);
            }
            // Fallback just in case to force the shop open
            else
            {
                System.Collections.IEnumerator shopRoutine = (System.Collections.IEnumerator)AccessTools.Method(typeof(NPC_Prawnathan), "OpenShopRoutine").Invoke(__instance, null);
                __instance.StartCoroutine(shopRoutine);
            }

            return false;
        }
    }

    [HarmonyPatch(typeof(CutsceneSpot),"Awake")]
    class CutscenePrawnathanFix
    {
        [HarmonyPostfix]
        static void Postfix(CutsceneSpot __instance)
        {
            if (__instance.name == "PrawnathanSpot")
            {
                Transform child = __instance.transform.GetChild(0);
                if (child)
                {
                    child.gameObject.SetActive(true);
                }
            }
        }
    }
}
