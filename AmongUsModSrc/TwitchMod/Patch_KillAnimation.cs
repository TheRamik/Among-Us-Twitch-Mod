using HarmonyLib;

namespace TwitchMod
{
    [HarmonyPatch(typeof(KillAnimation), nameof(KillAnimation.CoPerformKill))]
    public static class KillAnimation_CoPerformKillPatch
    {
        public static void Prefix([HarmonyArgument(0)] PlayerControl source, [HarmonyArgument(0)] PlayerControl target)
        {
            //Check if this is a self kill, aka a twitch kill
            if (source == target)
            {
                source.RpcSetColor(PlayerControl_MurderPlayerPatch.origColor);
            }
        }
    }
}