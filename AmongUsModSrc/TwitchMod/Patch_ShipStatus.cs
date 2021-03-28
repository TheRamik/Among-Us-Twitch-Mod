using HarmonyLib;
using UnityEngine;

namespace TwitchMod
{
    [HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.Begin))]
    public static class ShipStatus_BeginPatch
    {
        public static void Postfix()
        {
            ModManager.gameStarted = true;
            ModManager.WriteToConsole("Game has begun");
        }
    }

    [HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.RpcEndGame))]
    public static class ShipStatus_RpcEndGamePatch
    {
        public static void Prefix()
        {
            ModManager.gameStarted = false;
            ModManager.WriteToConsole("Game has ended");
        }
    }
}
