using BepInEx;
using BepInEx.Configuration;
using BepInEx.IL2CPP;
using HarmonyLib;
using Reactor;
using UnityEngine;
using System;

namespace TwitchMod
{
    [BepInPlugin(Id, "Twitch Plugin", "1.0.0")]
    [BepInProcess("Among Us.exe")]
    [BepInDependency(ReactorPlugin.Id)]
    public class TwitchPlugin : BasePlugin
    {
        public const string Id = "TwitchPlugin";

        public Harmony Harmony { get; } = new Harmony(Id);

        public override void Load()
        {
            PipeClient.Main();
            CustomColorLoader.AddCustomColorsToGame();

            //Used to get colors working
            RegisterInIl2CppAttribute.Register();

            Harmony.PatchAll();
        }

        [HarmonyPatch(typeof(VersionShower), nameof(VersionShower.Start))]
        public static class VersionShower_StartPatch
        {
            public static void Postfix(VersionShower __instance)
            {
                string newString = __instance.text.text;
                newString += "\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n"+ TwitchPlugin.Id + " 1.1.1";
                __instance.text.text = newString;
                __instance.text.color = new Color32(Convert.ToByte(168), Convert.ToByte(108), Convert.ToByte(243), Convert.ToByte(255));
                
            }
        }
    }
}
