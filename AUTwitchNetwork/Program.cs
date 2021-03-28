using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Serilog;
using TwitchLib.Api.Core.Enums;
using TwitchLib.PubSub;
using TwitchLib.PubSub.Events;
using TwitchLib.PubSub.Interfaces;
using TwitchLib.Api.Core.Exceptions;

namespace AmongUsTwitchNetwork
{
    public struct Settings
    {
        public Twitch twitch;
        public bool verbose;
    }

    public struct Twitch
    {
        public string channelName;
        public string channelId;
        public SettingAPI api;
        public Token token;
    }

    public struct SettingAPI
    {
        public string clientId;
        public string secret;
    }

    public struct Token
    {
        public string userAccess;
        public string refresh;
    }

    public struct RefreshResponse
    {
        public string access_token;
        public int expires_in;
        public string refresh_token;
    }

    /// <summary>
    /// Represents the running program
    /// </summary>
    public class Program
    {
        /// <summary>Serilog</summary>
        private static ILogger _logger;

        /// <summary>Settings</summary>
        public static Settings mySettings;

        /// <summary>Twitchlib Pubsub</summary>
        public static ITwitchPubSub PubSub;

        public static AmongUsTwitchAPI API;

        /// <summary>
        /// Requred Pipe instances
        /// </summary>
        public static NamedPipeServerStream pipeServer;
        public static StreamWriter sw;
        public static StreamReader sr;

        /// <summary>
        /// Main method
        /// </summary>
        /// <param name="args">Arguments</param>
        static void Main(string[] args)
        {
            var outputTemplate = "[{Timestamp:HH:mm:ss} {Level}] {Message}{NewLine}{Exception}";

            _logger = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .WriteTo.Console(outputTemplate: outputTemplate)
                .WriteTo.File("log/log_.txt", outputTemplate: outputTemplate, rollingInterval: RollingInterval.Day)
                .CreateLogger();

            using (StreamReader r = new StreamReader("Settings.json"))
            {
                string json = r.ReadToEnd();
                mySettings = JsonConvert.DeserializeObject<Settings>(json);
            }

            //run in async
            try
            {
                new Program()
                .MainAsync(args)
                .GetAwaiter()
                .GetResult();
            }
            catch(BadRequestException e)
            {
                Console.WriteLine($"Bad Request returned from API. Please check your settings.json for accuate info.\n");
                if (mySettings.verbose)
                { 
                    Console.WriteLine($"{e}");
                }
            }
            catch (BadScopeException e)
            {
                Console.WriteLine($"Bad Scope returned from API. Please check your settings.json for accuate info or" +
                    $"ensure that you have the correct scopes generated with your access token.\n");
                if (mySettings.verbose)
                {
                    Console.WriteLine($" {e}");
                }
            }
            catch (BadTokenException e)
            {
                Console.WriteLine($"Bad Token returned from API. Please check your settings.json for accuate access token" +
                    $"and refresh token.\n");
                if (mySettings.verbose)
                {
                    Console.WriteLine($" {e}");
                }
            }
            catch (Exception e)
            {
                if (mySettings.verbose)
                {
                    Console.WriteLine($"Failed for unknown reason. Enable verbose mode in your settings.json for more info.");
                    API.DisableAmongUsTwitchRewards();
                    Console.WriteLine($" {e}");
                }
            }
            Console.WriteLine("Press Enter to close the app....");
            Console.ReadLine();

        }

        /// <summary>
        /// Async main method
        /// </summary>
        /// <param name="args">Arguments</param>
        /// <returns>the Task</returns>
        private async Task MainAsync(string[] args)
        {
            var channelId = mySettings.twitch.channelId;
            var clientId = mySettings.twitch.api.clientId;
            var secret = mySettings.twitch.api.secret;
            var accessToken = mySettings.twitch.token.userAccess;

            // Set up twitchlib api
            API = new AmongUsTwitchAPI(mySettings);
            API.getAPI().Settings.ClientId = clientId;
            API.getAPI().Settings.Secret = secret;

            //Set up twitchlib pubsub
            PubSub = new TwitchPubSub();
            PubSub.OnListenResponse += OnListenResponse;
            PubSub.OnPubSubServiceConnected += OnPubSubServiceConnected;
            PubSub.OnPubSubServiceClosed += OnPubSubServiceClosed;
            PubSub.OnPubSubServiceError += OnPubSubServiceError;

            // Create Among Us Twitch Channel Points 
            await API.CreateAmongUsTwitchRewards();

            // Set up listeners
            ListenToRewards(channelId);

            // Connect to pubsub
            PubSub.Connect();

            // Sets up the pipe server
            pipeServer = new NamedPipeServerStream("AmongUsTwitchModPipe", PipeDirection.InOut, 4);
            sw = new StreamWriter(pipeServer);
            sr = new StreamReader(pipeServer);
            _logger.Information("NamedPipeServerStream object created.");

            // Wait for a client to connect
            _logger.Information("Waiting for client connection...");
            await pipeServer.WaitForConnectionAsync();

            _logger.Information("Client connected."); 

            // Keep the program going
            await Task.Delay(Timeout.Infinite);
        }

        #region Reward Events

        private void ListenToRewards(string channelId)
        {
            PubSub.OnRewardRedeemed += PubSub_OnRewardRedeemed;
            PubSub.OnCustomRewardCreated += PubSub_OnCustomRewardCreated;
            PubSub.OnCustomRewardDeleted += PubSub_OnCustomRewardDeleted;
            PubSub.OnCustomRewardUpdated += PubSub_OnCustomRewardUpdated;
            PubSub.ListenToRewards(channelId);
        }

        private void PubSub_OnCustomRewardUpdated(object sender, OnCustomRewardUpdatedArgs e)
        {
            _logger.Information($"Reward {e.RewardTitle} has been updated");
        }

        private void PubSub_OnCustomRewardDeleted(object sender, OnCustomRewardDeletedArgs e)
        {
            _logger.Information($"Reward {e.RewardTitle} has been removed");
        }

        private void PubSub_OnCustomRewardCreated(object sender, OnCustomRewardCreatedArgs e)
        {
            _logger.Information($"{e.RewardTitle} has been created");
            _logger.Information($"{e.RewardTitle} (\"{e.RewardId}\")");
        }

        private async void PubSub_OnRewardRedeemed(object sender, OnRewardRedeemedArgs e)
        {
            //Statuses can be:
            // "UNFULFILLED": when a user redeemed the reward
            // "FULFILLED": when a broadcaster or moderator marked the reward as complete
            if (e.Status == "UNFULFILLED")
            {
                _logger.Information($"{e.DisplayName} redeemed: {e.RewardTitle} " +
                    $"with prompt ${e.RewardPrompt.Split()}. With message: {e.Message}");
                await FulfillCustomReward(e);
                
            }

            if (e.Status == "FULFILLED")
            {
                _logger.Information($"Reward from {e.DisplayName} ({e.RewardTitle}) has been marked as complete");
            }
        }

        private async Task FulfillCustomReward(OnRewardRedeemedArgs e)
        {
            if (e.RewardTitle == Constants.KillPlayerString)
            {
                if (isValidCustomReward(e.RewardPrompt, e.Message))
                {
                    _logger.Information($"This is a valid kill. Sending over to Among Us Mod");
                    // If the pipe returns success, call UpdateCustomRewardRedemptionStatus with status fulfilled
                    SendToPipe("killplayer:" + e.RewardPrompt);
                    await API.UpdateRedemptionReward(e, CustomRewardRedemptionStatus.FULFILLED);
                }
                else
                {
                    _logger.Information($"This is not a valid kill. We are returning the points.");
                    await API.UpdateRedemptionReward(e, CustomRewardRedemptionStatus.CANCELED);
                }
            }
            else if (e.RewardTitle == Constants.KillRandomPlayerString)
            {
                _logger.Information($"Going to kill random player");
                // If the pipe returns success, call UpdateCustomRewardRedemptionStatus with status fulfilled
                SendToPipe("killrandomplayer");
                await API.UpdateRedemptionReward(e, CustomRewardRedemptionStatus.FULFILLED);
            }
            else if (e.RewardTitle == Constants.SwapPlayersString)
            {
                _logger.Information($"Going to swap players");
                // If the pipe returns success, call UpdateCustomRewardRedemptionStatus with status fulfilled
                SendToPipe("swapplayers");
                await API.UpdateRedemptionReward(e, CustomRewardRedemptionStatus.FULFILLED);
            }
        }

        private bool isValidCustomReward(string rewardPrompt, string rewardMessage)
        {
            bool isValid = false;
            var prompt = rewardPrompt.Split('\n');
            for (int i = 1; i <= prompt.Length - 1; i++)
            {
                _logger.Information($"{i}: {prompt[i]}");
                if (prompt[i] == rewardMessage)
                {
                    isValid = true;
                }
            }
            return isValid;
        }

        private void SendToPipe(string message)
        {
            try
            {
                // Read user input and send that to the client process.
                sw.AutoFlush = true;
                sw.WriteLine(message);
                pipeServer.WaitForPipeDrain();
            }
            // Catch the IOException that is raised if the pipe is broken
            // or disconnected.
            catch (IOException ex)
            {
                _logger.Error("ERROR: {0}", ex.Message);
            }

        }
    #endregion

    #region Pubsub events

    private void OnPubSubServiceError(object sender, OnPubSubServiceErrorArgs e)
        {
            _logger.Error($"{e.Exception.Message}");
        }

        private void OnPubSubServiceClosed(object sender, EventArgs e)
        {
            _logger.Information($"Connection closed to pubsub server");
        }

        private void OnPubSubServiceConnected(object sender, EventArgs e)
        {
            _logger.Information($"Connected to pubsub server");
            var oauth = mySettings.twitch.token.userAccess;
            PubSub.SendTopics(oauth);
        }

        private void OnListenResponse(object sender, OnListenResponseArgs e)
        {
            if (!e.Successful)
            {
                _logger.Error($"Failed to listen! Response{e.Response}");
            }
        }

        #endregion
    }
}