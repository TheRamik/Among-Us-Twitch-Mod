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

            //this are the short names
            string[] shortColorNames =
            {
			//we need to add the vanilla colors so that the new color won't replace it
			"RED",
            "DBLUE",
            "GRN",
            "PINK",
            "ORNG",
            "YLOW",
            "BLAK",
            "WHTE",
            "PURP",
            "BRWN",
            "CYAN",
            "LIME",
			//The twitch color
			"TWCH",

            };
            //this are the long names
            string[] colorNames =
            {
            "Red",
            "Dark Blue",
            "Green",
            "Pink",
            "Orange",
            "Yellow",
            "Black",
            "White",
            "Purple",
            "Brown",
            "Cyan",
            "Lime",
			//The Twitch color(with full name)
			"Twitch"

        };
            Color32[] playerColors =
            {
				//this one are the colors
			new Color32(240, 19, 19, byte.MaxValue),
            new Color32(19, 46, 210, byte.MaxValue),
            new Color32(0, 92, 23, byte.MaxValue),
            new Color32(238, 84, 187, byte.MaxValue),
            new Color32(250, 167, 0, byte.MaxValue),
            new Color32(246, 246, 87, byte.MaxValue),
            new Color32(63, 71, 78, byte.MaxValue),
            new Color32(215, 225, 241, byte.MaxValue),
            new Color32(107, 47, 188, byte.MaxValue),
            new Color32(77, 50, 21, byte.MaxValue),
            new Color32(56, 254, 220, byte.MaxValue),
            new Color32(80, 240, 57, byte.MaxValue),
			//The Twitch color
			//new Color32(145, 71, 255, byte.MaxValue),
            new Color32(164, 108, 243, byte.MaxValue),
            };
            Color32[] shadowColors =
            {
            new Color32(122, 8, 56, byte.MaxValue),
            new Color32(9, 21, 142, byte.MaxValue),
            new Color32(0, 56, 14, byte.MaxValue),
            new Color32(172, 43, 174, byte.MaxValue),
            new Color32(180, 62, 21, byte.MaxValue),
            new Color32(195, 136, 34, byte.MaxValue),
            new Color32(30, 31, 38, byte.MaxValue),
            new Color32(132, 149, 192, byte.MaxValue),
            new Color32(59, 23, 124, byte.MaxValue),
            new Color32(56, 37, 16, byte.MaxValue),
            new Color32(36, 168, 190, byte.MaxValue),
            new Color32(21, 168, 66, byte.MaxValue),
            //Twitch shadow color
            //new Color32(86, 42, 150, byte.MaxValue),
            new Color32(111, 45, 204, byte.MaxValue),
            };
            //this one is the one who loads the colors
            //Palette.ShortColorNames = shortColorNames;
            Palette.PlayerColors = playerColors;
            Palette.ShadowColors = shadowColors;
            //MedScanMinigame.ColorNames = colorNames;
            //Telemetry.ColorNames = colorNames;

            //a default code
            RegisterInIl2CppAttribute.Register();

            Harmony.PatchAll();
        }

        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.MurderPlayer))]
        public static class PlayerControl_MurderPlayerPatch
        {
            public static bool wasImpostor;
            public static byte origColor;

            public static void Prefix(PlayerControl __instance)
            {
                //Save original impostor status
                wasImpostor = __instance.Data.IsImpostor;
                origColor = __instance.Data.ColorId;
                //Set the player to an impostor so they can kill themselves
                __instance.Data.IsImpostor = true;
                __instance.RpcSetColor(12);
            }

            public static void Postfix(PlayerControl __instance)
            {
                //Restore original impostor status
                __instance.Data.IsImpostor = wasImpostor;
                __instance.RpcSetColor(origColor);
            }
        }

        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
        public static class ExamplePatch
        {
            public static void Postfix(PlayerControl __instance)
            {
                __instance.nameText.Text = "SUSMAN";
                __instance.RpcSetColor(12);

                if (ModManager.playerInfoDict.Count == 0)
                {
                    ModManager.UpdatePlayerDicts();
                }

                if (Input.GetKeyDown(KeyCode.G))
                {
                    System.Console.WriteLine("G pressed");
                }
                if (Input.GetKeyDown(KeyCode.Alpha0))
                {
                    System.Console.WriteLine("0 pressed");
                    ModManager.MurderPlayerDebug(0);
                }
                if (Input.GetKeyDown(KeyCode.Alpha1))
                {
                    System.Console.WriteLine("1 pressed");
                    ModManager.MurderPlayerDebug(1);
                }
                if (Input.GetKeyDown(KeyCode.Alpha2))
                {
                    System.Console.WriteLine("2 pressed");
                    ModManager.MurderPlayerDebug(2);
                }
                if (Input.GetKeyDown(KeyCode.Alpha3))
                {
                    System.Console.WriteLine("3 pressed");
                    ModManager.MurderPlayerDebug(3);
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

        [HarmonyPatch(typeof(GameStartManager), nameof(GameStartManager.Update))]
        public static class TestPatch
        {

            public static testClass test = new testClass();

            public static void Postfix()
            {
                if (GameStartManager.Instance.startState == GameStartManager.StartingStates.Starting)
                {
                    //foreach (GameData.PlayerInfo playerInfo in GameData.Instance.AllPlayers)
                    //{
                    //    System.Console.WriteLine(playerInfo.PlayerId);
                    //    System.Console.WriteLine(playerInfo.PlayerName);
                    //    testMethod(playerInfo.PlayerName);
                    //    foreach (PlayerControl playerControl in PlayerControl.AllPlayerControls)
                    //    {
                    //        System.Console.WriteLine(playerControl.PlayerId);
                    //        if (playerControl.PlayerId == playerInfo.PlayerId)
                    //        {
                    //            System.Console.WriteLine("YOPOOO");
                    //            playerControl.Die(DeathReason.Kill);
                    //        }
                    //    }

                    //}
                    
                }

                //Maybe later have something that sets a bool when the game is over and when it starts?
                //Like playerDicts set = true? on game start and false on game end?

            }
            public static void testMethod(string playerName)
            {
                test.testMethod(playerName);
            }
        }

        public class testClass
        {
            public void testMethod(string playerName)
            {
                //System.Console.WriteLine("Method works! Got " + playerName);
                //System.Console.WriteLine("No for real it works!");
            }
        }

        public static class ModManager
        {
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

                System.Console.WriteLine("Trying to kill " + playerNames[playerNum]);
                playerControlDict[playerNames[playerNum]].RpcMurderPlayer(playerControlDict[playerNames[playerNum]]);
            }

            public static void UpdatePlayerDicts()
            {
                System.Console.WriteLine("Fetching/refreshing player data...");
                localPlayer = null;
                playerInfoDict.Clear();
                playerControlDict.Clear();
                foreach (PlayerControl player in PlayerControl.AllPlayerControls)
                {
                    GameData.PlayerInfo playerInfo = GameData.Instance.GetPlayerById(player.PlayerId);
                    if (PlayerControl.LocalPlayer == player)
                    {
                        localPlayer = playerInfo;
                        System.Console.WriteLine(playerInfo.PlayerName + " is the local player, aka streamer");
                        System.Console.WriteLine(playerInfo.PlayerName + "==" + localPlayer.PlayerName + "?");
                    }
                    else
                    {
                        System.Console.WriteLine(playerInfo.PlayerName + " is not the local player.");
                    }
                    playerInfoDict[playerInfo.PlayerName] = playerInfo;
                    playerControlDict[playerInfo.PlayerName] = player;
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

        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.Start))]
        public static class PlayerControl_StartPatch
        {
            //public static void Postfix(PlayerControl __instance)
            //{
            //    System.Console.WriteLine("Called Start");
            //    System.Console.WriteLine(__instance.PlayerId);
            //    System.Console.WriteLine(__instance.AmOwner);
            //    if (__instance.AmOwner)
            //    {
            //        GameData.PlayerInfo localInfo = (GameData.Instance.GetPlayerById(__instance.PlayerId));
            //        System.Console.Write(localInfo.PlayerId);
            //        System.Console.Write(localInfo.PlayerName);
            //        ModManager.localPlayer = GameData.Instance.GetPlayerById(__instance.PlayerId);
            //    }
            //    if (ModManager.localPlayer != null)
            //    {
            //        System.Console.WriteLine(ModManager.localPlayer.PlayerName);
            //    }
            //}
        }
    }
}
