using BepInEx;
using BepInEx.Configuration;
using BepInEx.IL2CPP;
using HarmonyLib;
using Reactor;
using System.Collections.Generic;
using UnityEngine;

namespace TestMod
{
    [BepInPlugin(Id)]
    [BepInProcess("Among Us.exe")]
    [BepInDependency(ReactorPlugin.Id)]
    public class TwitchPlugin : BasePlugin
    {
        public const string Id = "TwitchPlugin";

        public Harmony Harmony { get; } = new Harmony(Id);

        public ConfigEntry<string> Name { get; private set; }

        public override void Load()
        {
            Name = Config.Bind("Fake", "Name", "SUSMAN");

            CustomColorLoader.AddCustomColorsToGame();

            //Used to get colors working
            RegisterInIl2CppAttribute.Register();

            Harmony.PatchAll();
        }

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
                if(Input.GetKeyDown(KeyCode.BackQuote))
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

        public static class ModManager
        {
            public static bool debugMode = false;
            public static GameData.PlayerInfo localPlayer;
            public static Dictionary<string, GameData.PlayerInfo> playerInfoDict = new Dictionary<string, GameData.PlayerInfo>();
            public static Dictionary<string, PlayerControl> playerControlDict = new Dictionary<string, PlayerControl>();

            /// <summary>
            /// Kills the selected player based on an index of a list of playerNames
            /// </summary>
            /// <param name="playerNum"></param>
            public static void MurderPlayerDebug(int playerNum)
            {
                List<string> playerNames = new List<string>();
                foreach (string name in playerInfoDict.Keys) playerNames.Add(name);

                WriteToConsole("Trying to kill " + playerNames[playerNum]);
                playerControlDict[playerNames[playerNum]].RpcMurderPlayer(playerControlDict[playerNames[playerNum]]);
            }

            public static void UpdatePlayerDicts()
            {
                WriteToConsole("Fetching/refreshing player data...");
                localPlayer = null;
                playerInfoDict.Clear();
                playerControlDict.Clear();
                foreach (PlayerControl player in PlayerControl.AllPlayerControls)
                {
                    GameData.PlayerInfo playerInfo = GameData.Instance.GetPlayerById(player.PlayerId);
                    if (PlayerControl.LocalPlayer == player)
                    {
                        localPlayer = playerInfo;
                        WriteToConsole(playerInfo.PlayerName + " is the local player, aka streamer");
                        WriteToConsole(playerInfo.PlayerName + "==" + localPlayer.PlayerName + "?");
                    }
                    else
                    {
                        WriteToConsole(playerInfo.PlayerName + " is not the local player.");
                    }
                    playerInfoDict[playerInfo.PlayerName] = playerInfo;
                    playerControlDict[playerInfo.PlayerName] = player;
                }
            }

            public static void ToggleDebugMode()
            {
                debugMode = !debugMode;
                if (debugMode)
                {
                    WriteToConsole("Debug Mode enabled. Please use for dev purposes only.");
                }
                else
                {
                    System.Console.WriteLine("Twitch Mod: Debug Mode disabled.");
                }
            }

            public static void WriteToConsole(string toOutput)
            {
                if (debugMode)
                {
                    System.Console.WriteLine("Twitch Mod: " + toOutput);
                }
            }
        }

        [HarmonyPatch(typeof(GameStartManager), nameof(GameStartManager.FinallyBegin))]
        public static class GameStartManager_FinallyBeginPatch
        {
            public static void Postfix()
            {
                ModManager.UpdatePlayerDicts();
            }
        }

        [HarmonyPatch(typeof(KillAnimation), nameof(KillAnimation.CoPerformKill))]
        public static class KillAnimation_CoPerformKillPatch
        {
            public static void Prefix([HarmonyArgument(0)] PlayerControl source, [HarmonyArgument(0)] PlayerControl target)
            {
                //Check if this is a self kill, aka a twitch kill
                if(source == target)
                {
                    source.RpcSetColor(PlayerControl_MurderPlayerPatch.origColor);
                }
            }
        }

        public static class CustomColorLoader
        {
            public class CustomColor
            {
                public StringNames shortColorName;
                public StringNames fullColorName;
                public Color32 mainColor;
                public Color32 shadowColor;

                public CustomColor(string shortColorName, string fullColorName, Color32 mainColor, Color32 shadowColor)
                {
                    this.shortColorName = CustomStringName.Register(shortColorName);
                    this.fullColorName = CustomStringName.Register(fullColorName);
                    this.mainColor = mainColor;
                    this.shadowColor = shadowColor;
                }
            }

            public static List<CustomColor> customColors = new List<CustomColor>();
            
            
            private static void InitializeCustomColors()
            {
                CustomColor twitchColor = new CustomColor("TWCH", "Twitch", new Color32(164, 108, 243, byte.MaxValue), new Color32(111, 45, 204, byte.MaxValue));
                customColors.Add(twitchColor);
            }

            public static void AddCustomColorsToGame()
            {
                InitializeCustomColors();

                //Allocate array memory for custom colors
                int colorCount = Palette.PlayerColors.Length;
                UnhollowerBaseLib.Il2CppStructArray<Color32> playerColors = new UnhollowerBaseLib.Il2CppStructArray<Color32>(colorCount + customColors.Count);
                UnhollowerBaseLib.Il2CppStructArray<Color32> shadowColors = new UnhollowerBaseLib.Il2CppStructArray<Color32>(colorCount + customColors.Count);
                UnhollowerBaseLib.Il2CppStructArray<StringNames> shortColorNames = new UnhollowerBaseLib.Il2CppStructArray<StringNames>(colorCount + customColors.Count);
                UnhollowerBaseLib.Il2CppStructArray<StringNames> fullColorNames = new UnhollowerBaseLib.Il2CppStructArray<StringNames>(colorCount + customColors.Count);

                //Add orig colors to array
                for(int i = 0; i < colorCount; i++)
                {
                    playerColors[i] = Palette.PlayerColors[i];
                    shadowColors[i] = Palette.ShadowColors[i];
                    shortColorNames[i] = Palette.ShortColorNames[i];
                    fullColorNames[i] = Palette.ColorNames[i];
                }

                //Add cusotm colors to array
                for (int i = 0; i < customColors.Count; i++)
                {
                    playerColors[colorCount + i] = customColors[i].mainColor;
                    shadowColors[colorCount + i] = customColors[i].shadowColor;
                    shortColorNames[colorCount + i] = customColors[i].shortColorName;
                    fullColorNames[colorCount + i] = customColors[i].fullColorName;
                }

                //Update palette
                Palette.PlayerColors = playerColors;
                Palette.ShadowColors = shadowColors;
                Palette.ShortColorNames = shortColorNames;
                Palette.ColorNames = fullColorNames;
                MedScanMinigame.ColorNames = fullColorNames; 
            }
        }
    }
}
