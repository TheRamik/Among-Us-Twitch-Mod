using System;
using System.IO;
using System.IO.Pipes;

namespace TwitchMod
{
    class PipeClient
    {
        public static async void Main()
        {
            using (NamedPipeClientStream pipeClient =
                new NamedPipeClientStream(".", "testpipe", PipeDirection.In))
            {

                // Connect to the pipe or wait until the pipe is available.
                System.Console.Write("Attempting to connect to pipe...");
                try { 
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
                using (StreamReader sr = new StreamReader(pipeClient))
                {
                    // Display the read text to the console
                    string temp;
                    while ((temp = sr.ReadLine()) != null)
                    {
                        System.Console.WriteLine("Received from server: {0}", temp);
                        if (temp == "deeznuts")
                        {
                            ModManager.killPlayer = true;
                        }
                    }
                }
            }
        }
    }
}
