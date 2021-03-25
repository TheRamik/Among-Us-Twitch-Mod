using HarmonyLib;
using UnityEngine;

namespace TwitchMod
{
    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.MurderPlayer))]
    public static class PlayerControl_MurderPlayerPatch
    {
        public static bool wasImpostor;
        //Set to 0 to avoid potential nastiness with KillAnimation patch
        public static byte origColor = 0;

        public static void Prefix(PlayerControl __instance, [HarmonyArgument(0)] PlayerControl target)
        {
            //Check if this is a self kill, aka a twitch kill
            if (target == __instance)
            {
                //Save original impostor status
                wasImpostor = __instance.Data.IsImpostor;
                origColor = __instance.Data.ColorId;
                //Set the player to an impostor so they can kill themselves
                __instance.Data.IsImpostor = true;
                __instance.RpcSetColor(12);
            }
        }

        public static void Postfix(PlayerControl __instance, [HarmonyArgument(0)] PlayerControl target)
        {
            //Check if this is a self kill, aka a twitch kill
            if (target == __instance)
            {
                //Restore original color and impostor status
                __instance.Data.IsImpostor = wasImpostor;
                __instance.RpcSetColor(origColor);
            }
        }
    }

    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
    public static class PlayerControl_FixedUpdatePatch
    {
        public static void Postfix(PlayerControl __instance)
        {
            //__instance.RpcSetColor(12);
            if (Input.GetKeyDown(KeyCode.Equals))
            {
                ModManager.ToggleDebugMode();
            }

            if (ModManager.playerInfoDict.Count == 0)
            {
                ModManager.UpdatePlayerDicts();
            }

            if (ModManager.debugMode)
            {
                if (Input.GetKeyDown(KeyCode.Alpha0))
                {
                    ModManager.WriteToConsole("0 pressed");
                    ModManager.MurderPlayerDebug(0);
                }
                if (Input.GetKeyDown(KeyCode.Alpha1))
                {
                    ModManager.WriteToConsole("1 pressed");
                    ModManager.MurderPlayerDebug(1);
                }
                if (Input.GetKeyDown(KeyCode.Alpha2))
                {
                    ModManager.WriteToConsole("2 pressed");
                    ModManager.MurderPlayerDebug(2);
                }
                if (Input.GetKeyDown(KeyCode.Alpha3))
                {
                    ModManager.WriteToConsole("3 pressed");
                    ModManager.MurderPlayerDebug(3);
                }
            }
        }
    }
}