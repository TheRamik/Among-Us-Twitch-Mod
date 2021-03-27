using System;
using System.IO;
using System.Linq;
using System.IO.Pipes;

namespace TwitchMod
{
    class PipeClient
    {
        public static async void Main()
        {
            using (NamedPipeClientStream pipeClient =
                new NamedPipeClientStream(".", "testpipe", PipeDirection.InOut, PipeOptions.Asynchronous))
            {

                // Connect to the pipe or wait until the pipe is available.
                System.Console.Write("Attempting to connect to pipe...");
                try
                {
                    await pipeClient.ConnectAsync(10000);
                }
                catch (Exception e)
                {
                    System.Console.Write("Failed to connectasync. Let's move on");
                    System.Console.Write(e);
                    return;
                }

                System.Console.WriteLine("Connected to pipe.");
                System.Console.WriteLine("There are currently {0} pipe server instances open.",
                   pipeClient.NumberOfServerInstances);
                StreamWriter sw = new StreamWriter(pipeClient);
                StreamReader sr = new StreamReader(pipeClient);
                // Display the read text to the console
                while (true)
                {
                    string message;
                    while ((message = await sr.ReadLineAsync()) != null)
                    {
                        System.Console.WriteLine("Received from server: {0}", message);
                        if (message.StartsWith("killplayer:"))
                        {
                            var optionNames = message.Split(':').ToList();
                            optionNames.RemoveAt(0);
                            var playerName = optionNames.First();
                            ModManager.AddTwitchCommand(new TwitchCommandInfo(TwitchCommand.KillRandomPlayer, playerName));
                            // sw.WriteLine("Player killed");
                        }
                        else if (message == "killrandomplayer")
                        {
                            ModManager.AddTwitchCommand(new TwitchCommandInfo(TwitchCommand.KillRandomPlayer));
                        }
                        else if (message == "swapplayers")
                        {
                            ModManager.AddTwitchCommand(new TwitchCommandInfo(TwitchCommand.SwapPlayers));
                        }
                        else
                        {
                            System.Console.WriteLine($"Failed to understand request: {message}");
                        }
                        sw.Flush();
                        pipeClient.WaitForPipeDrain();
                    }
                }
            }
        }
    }
}
