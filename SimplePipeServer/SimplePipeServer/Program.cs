using System;
using System.IO;
using System.IO.Pipes;
using System.Threading.Tasks;

class PipeServer
{
    static void Main()
    {
        MainAsync().GetAwaiter().GetResult();
    }

    static async Task MainAsync()
    {
        using (NamedPipeServerStream pipeServer =
            new NamedPipeServerStream("testpipe", PipeDirection.InOut))
        {
            Console.WriteLine("NamedPipeServerStream object created.");

            // Wait for a client to connect
            Console.Write("Waiting for client connection...");
            pipeServer.WaitForConnection();

            Console.WriteLine("Client connected.");
            try
            {
                // Read user input and send that to the client process.
                using (StreamWriter sw = new StreamWriter(pipeServer))
                {
                    sw.AutoFlush = true;
                    //Read();
                    while (true)
                    {
                        Console.Write("Enter text: ");
                        sw.WriteLine(Console.ReadLine());
                        Console.Write("Awaiting response...");
                        pipeServer.WaitForPipeDrain();
                        using (StreamReader sr = new StreamReader(pipeServer))
                        {
                            // Display the read text to the console
                            string temp;
                            temp = await sr.ReadLineAsync();
                            Console.WriteLine("Client responded: " + temp);
                        }
                    }
                }
            }
            // Catch the IOException that is raised if the pipe is broken
            // or disconnected.
            catch (IOException e)
            {
                Console.WriteLine("ERROR: {0}", e.Message);
            }
        }
    }    
}