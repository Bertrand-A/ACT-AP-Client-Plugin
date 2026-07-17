using HarmonyLib;

namespace ACTAP
{
    [HarmonyPatch(typeof(AudioManager), nameof(AudioManager.OnGamePaused))]
    internal class HideWorldMarkers
    {
        [HarmonyPostfix]
        public static void Postfix(bool paused)
        {
            Plugin.RenderWorldMarkersTemp = !paused;
        }
    }
}
