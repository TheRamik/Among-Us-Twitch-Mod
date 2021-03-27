using System.Collections.Generic;
using UnityEngine;

namespace TwitchMod
{
    public static class ModManager
    {

        public static bool debugMode = true;
        public static GameData.PlayerInfo localPlayer;
        public static Dictionary<string, GameData.PlayerInfo> playerInfoDict = new Dictionary<string, GameData.PlayerInfo>();
        public static Dictionary<string, PlayerControl> playerControlDict = new Dictionary<string, PlayerControl>();

        public static string playerNameToKill = "";
        public static bool killingPlayer = false;
        public static Queue<TwitchCommandInfo> twitchCommandQueue = new Queue<TwitchCommandInfo>();

        /// <summary>
        /// Kills the selected player based on an index of a list of playerNames
        /// </summary>
        /// <param name="playerNum"></param>
        public static void MurderPlayerDebug(int playerNum)
        {
            List<string> playerNames = getPlayerNames();

            try
            {
                WriteToConsole("Trying to kill " + playerNames[playerNum]);
                TwitchMurderPlayer(playerControlDict[playerNames[playerNum]]);
            }
            catch(System.ArgumentOutOfRangeException)
            {
                WriteToConsole("Kill failed, no player #" + playerNum);
            }
        }

        public static void MurderPlayerByName(string playerName)
        {
            //TODO: make string lowercase?
            PlayerControl toKill;
            WriteToConsole("Received command to try killing \"" + playerName + "\"");
            //TODO: Send success or failure back to server
            if(playerControlDict.TryGetValue(playerName, out toKill))
            {
                WriteToConsole("Trying to kill \"" + playerName + "\"");
                TwitchMurderPlayer(toKill);
            }
            else
            {
                WriteToConsole("Error: No player named \"" + playerName + "\"");
                SendMessageToServer("Kill Failed: No player named \"" + playerName + "\"");
            }
        }

        public static void MurderRandomPlayer()
        {
            WriteToConsole("Trying to kill a random player");
            if(playerControlDict.Count <= 0)
            {
                WriteToConsole("Kill failed, no players detected");
                return;
            }

            List<string> playerNames = getPlayerNames();
            playerNames.Shuffle();
            foreach(string playerName in playerNames)
            {
                //Check to see if this player is killable
                if (!playerInfoDict[playerName].IsDead)
                {
                    WriteToConsole("Trying to kill \"" + playerName + "\"");
                    TwitchMurderPlayer(playerControlDict[playerName]);
                    return;
                }
            }
            //If we get here, all the players are dead
            WriteToConsole("Kill failed, all players are dead or the player data is out of date");
        }

        public static List<string> getPlayerNames()
        {
            List<string> playerNames = new List<string>();
            foreach (string name in playerInfoDict.Keys) playerNames.Add(name);
            return playerNames;
        }

        private static void TwitchMurderPlayer(PlayerControl toKill)
        {
            killingPlayer = true;
            toKill.RpcMurderPlayer(toKill);
        }

        /// <summary>
        /// Updates the player dictionary info to contain all of the current players
        /// </summary>
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

        public static void SendMessageToServer(string message)
        {
            WriteToConsole("Sending message to server: " + message);
        }

        public static void AddTwitchCommand(TwitchCommandInfo command)
        {
            twitchCommandQueue.Enqueue(command);
        }

        public static bool HasTwitchCommandQueue()
        {
            return twitchCommandQueue.Count > 0;
        }

        public static void RunTwitchCommandQueue()
        {
            WriteToConsole("Executing new twitch command queue:");
            while(twitchCommandQueue.Count > 0)
            {
                TwitchCommandInfo commandInfo = twitchCommandQueue.Dequeue();
                switch(commandInfo.command)
                {
                    case TwitchCommand.KillRandomPlayer:
                        MurderRandomPlayer();
                        break;
                    case TwitchCommand.SwapPlayers:
                        RandomlySwapAllPlayers();
                        break;
                    case TwitchCommand.KillNamedPlayer:
                        MurderPlayerByName(commandInfo.infoString);
                        break;
                    default:
                        break;
                }    
            }
        }

        public static void SwapPlayerPositions(PlayerControl player1, PlayerControl player2)
        {
            Vector2 originPos1 = player1.transform.position;
            Vector2 originPos2 = player2.transform.position;

            WriteToConsole("Attempting swap");
            player1.NetTransform.RpcSnapTo(originPos2);
            player2.NetTransform.RpcSnapTo(originPos1);
            WriteToConsole("Swap successful");
        }

        public static void RandomlySwapAllPlayers()
        {
            WriteToConsole("Trying to randomly swap all players");
            if (playerControlDict.Count <= 0)
            {
                WriteToConsole("Swap failed, no players detected");
                return;
            }

            List<string> playerNames = getPlayerNames();
            playerNames.Shuffle();
            for(int i = 0; i < playerNames.Count - 1; i++)
            {
                SwapPlayerPositions(playerControlDict[playerNames[i]], playerControlDict[playerNames[i + 1]]);
            }
            WriteToConsole("All players successfully swapped!");
        }

        public static void PlayerSwapDebug()
        {
            if(playerControlDict.Count < 2)
            {
                WriteToConsole("Debug swap failed, less than 2 players");
            }
            List<string> playerNames = getPlayerNames();
            WriteToConsole(string.Format("Swapping {0} and {1}", playerNames[0], playerNames[1]));
            SwapPlayerPositions(playerControlDict[playerNames[0]], playerControlDict[playerNames[1]]);
        }
    }
}