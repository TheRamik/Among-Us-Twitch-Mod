using System.Collections.Generic;

namespace TwitchMod
{
    public static class ModManager
    {
        public static bool debugMode = true;
        public static GameData.PlayerInfo localPlayer;
        public static Dictionary<string, GameData.PlayerInfo> playerInfoDict = new Dictionary<string, GameData.PlayerInfo>();
        public static Dictionary<string, PlayerControl> playerControlDict = new Dictionary<string, PlayerControl>();

        public static bool killPlayer = false;
        public static string playerNameToKill = "";
        public static bool killingPlayer = false;

        /// <summary>
        /// Kills the selected player based on an index of a list of playerNames
        /// </summary>
        /// <param name="playerNum"></param>
        public static void MurderPlayerDebug(int playerNum)
        {
            List<string> playerNames = new List<string>();
            foreach (string name in playerInfoDict.Keys) playerNames.Add(name);

            try
            {
                WriteToConsole("Trying to kill " + playerNames[playerNum]);
                killingPlayer = true;
                playerControlDict[playerNames[playerNum]].RpcMurderPlayer(playerControlDict[playerNames[playerNum]]);
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
                killingPlayer = true;
                //TODO: Find way to check if the command was not successful because the player was already dead
                toKill.RpcMurderPlayer(toKill);     
            }
            else
            {
                WriteToConsole("Error: No player named \"" + playerName + "\"");
                SendMessageToServer("Kill Failed: No player named \"" + playerName + "\"");
            }
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
    }
}