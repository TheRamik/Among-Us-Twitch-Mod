using HarmonyLib;

namespace TwitchMod
{
    [HarmonyPatch(typeof(GameStartManager), nameof(GameStartManager.FinallyBegin))]
    public static class GameStartManager_FinallyBeginPatch
    {
        public static void Postfix()
        {
            ModManager.UpdatePlayerDicts();
        }
    }
}