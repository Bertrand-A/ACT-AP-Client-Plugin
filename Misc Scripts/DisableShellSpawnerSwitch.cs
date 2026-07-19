// using System.Collections.Generic;
// using HarmonyLib;
// using UnityEngine;
//
// namespace ACTAP
// {
//     [HarmonyPatch(typeof(InteractableEntity), "Awake")]
//     static class DisableShellSpawnerSwitch
//     {
//         public static bool switchesEnabled = false;
//         public static readonly List<GameObject> switchObjects = new();
//
//         [HarmonyPostfix]
//         static void Postfix(InteractableEntity __instance)
//         {
//             if (__instance is ScuttleportShellSpawnerSwitch && (Plugin.connection.session != null || Plugin.debugMode))
//             {
//                 Animator animator = Traverse.Create(__instance).Field("animator").GetValue<Animator>();
//                 if (animator != null)
//                 {
//                     Track(animator.gameObject);
//                 }
//                 Track(__instance.gameObject);
//                 Debug.Log("Shell Spawner " + (switchesEnabled ? "Enabled" : "Disabled"));
//             }
//         }
//
//         static void Track(GameObject obj)
//         {
//             switchObjects.RemoveAll(o => o == null);
//             if (!switchObjects.Contains(obj))
//             {
//                 switchObjects.Add(obj);
//             }
//             obj.SetActive(switchesEnabled);
//         }
//
//         public static void SetSwitchesEnabled(bool enabled)
//         {
//             switchesEnabled = enabled;
//             switchObjects.RemoveAll(o => o == null);
//             foreach (GameObject obj in switchObjects)
//             {
//                 obj.SetActive(enabled);
//             }
//             Debug.Log("Shell Spawner Switches " + (enabled ? "Enabled" : "Disabled"));
//         }
//     }
//
//     //F7 debug toggle
//     [HarmonyPatch(typeof(Player), "Update")]
//     static class ToggleShellSpawnerSwitch
//     {
//         [HarmonyPostfix]
//         static void Postfix()
//         {
//             if ((Plugin.connection.session != null || Plugin.debugMode) && Input.GetKeyDown(KeyCode.F7))
//             {
//                 DisableShellSpawnerSwitch.SetSwitchesEnabled(!DisableShellSpawnerSwitch.switchesEnabled);
//             }
//         }
//     }
// }
