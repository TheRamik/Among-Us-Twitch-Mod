using HarmonyLib;
using UnityEngine;

namespace TwitchMod
{
    [HarmonyPatch(typeof(PlayerTab), nameof(PlayerTab.OnEnable))]
    public static class PlayerTab_OnEnablePatch
    {
        public static void Postfix(PlayerTab __instance)
        {
            //Remove the custom twitch color from player tab
            Object.Destroy(__instance.ColorChips[__instance.ColorChips.Count - 1].gameObject);
            __instance.ColorChips.RemoveAt(__instance.ColorChips.Count - 1);
        }
    }
}