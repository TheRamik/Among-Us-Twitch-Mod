using BepInEx;
using BepInEx.Configuration;
using BepInEx.IL2CPP;
using HarmonyLib;
using Reactor;
using UnityEngine;

namespace TwitchMod
{
    [BepInPlugin(Id)]
    [BepInProcess("Among Us.exe")]
    [BepInDependency(ReactorPlugin.Id)]
    public class TwitchPlugin : BasePlugin
    {
        public const string Id = "TwitchPlugin";

        public Harmony Harmony { get; } = new Harmony(Id);

        public override void Load()
        {
            CustomColorLoader.AddCustomColorsToGame();

            //Used to get colors working
            RegisterInIl2CppAttribute.Register();

            Harmony.PatchAll();
        }
    }
}
