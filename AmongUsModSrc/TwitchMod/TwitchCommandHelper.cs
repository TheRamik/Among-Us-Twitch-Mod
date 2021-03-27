using System;

namespace TwitchMod
{
    public enum TwitchCommand
    {
        KillNamedPlayer,
        KillRandomPlayer,
        RadomizePositions
    }

    public struct TwitchCommandInfo
    {
        public TwitchCommand command;
        public string infoString;

        public TwitchCommandInfo(TwitchCommand command)
        {
            this.command = command;
            infoString = "";
        }

        public TwitchCommandInfo(TwitchCommand command, string info)
        {
            this.command = command;
            infoString = info;
        }
    }
}
