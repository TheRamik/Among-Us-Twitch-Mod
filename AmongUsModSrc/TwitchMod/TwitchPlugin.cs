using BepInEx;
using System.Threading;
using System.Threading.Tasks;
using BepInEx.Configuration;
using BepInEx.IL2CPP;
using HarmonyLib;
using Reactor;
using UnityEngine;

namespace TwitchMod
{
    [BepInPlugin(Id, "Twitch Plugin", "1.0.0")]
    [BepInProcess("Among Us.exe")]
    [BepInDependency(ReactorPlugin.Id)]
    public class TwitchPlugin : BasePlugin
    {
        public const string Id = "TwitchPlugin";

        public Harmony Harmony { get; } = new Harmony(Id);

        private async Task MainAsync()
        {
            System.Console.WriteLine("Before AmongUsTwitchAPI");
            AmongUsTwitchAPI.InitAmongUsTwitchAPI();
            AmongUsTwitchAPI.initializeRewards();
            System.Console.WriteLine("After AmongUsTwitchAPI");
            var twitchListener = new TwitchListener();
            twitchListener.ListenToRewards(AmongUsTwitchAPI.GetChannelId());
            twitchListener.Connect();

            //Keep the program going
            await Task.Delay(Timeout.Infinite);

        }

        public override void Load()
        {
            CustomColorLoader.AddCustomColorsToGame();
            System.Console.WriteLine("Before MainAsync");

            MainAsync().GetAwaiter().GetResult();
            System.Console.WriteLine("After AmongUsTwitchAPI");
            //Used to get colors working
            RegisterInIl2CppAttribute.Register();

            Harmony.PatchAll();
        }

        [HarmonyPriority(Priority.Low)] // to show this message last
        [HarmonyPatch(typeof(VersionShower), nameof(VersionShower.Start))]
        public static class VersionShower_StartPatch
        {
            public static void Postfix(VersionShower __instance)
            {
                string newString = __instance.text.Text;
                newString += "\n\n\n\n\n\n\n[A86CF3FF]" + TwitchPlugin.Id + " 1.0.0[]";
                __instance.text.Text = newString;
                
            }
        }
    }
}
