using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Serilog;
using TwitchLib.Api;
using TwitchLib.Api.Core.Enums;
using TwitchLib.Api.Core.Exceptions;
using TwitchLib.Api.Helix.Models.ChannelPoints;
using TwitchLib.Api.Helix.Models.ChannelPoints.CreateCustomReward;
using TwitchLib.Api.Helix.Models.ChannelPoints.UpdateCustomReward;
using TwitchLib.Api.Helix.Models.ChannelPoints.UpdateCustomRewardRedemptionStatus;
using TwitchLib.Api.Interfaces;
using TwitchLib.PubSub;
using TwitchLib.PubSub.Events;
using TwitchLib.PubSub.Interfaces;

public struct Settings
{
    public Twitch twitch;
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

namespace AUTwitchNetwork
{

    /// <summary>
    /// Represents the example bot
    /// </summary>
    public class Program
    {
        /// <summary>Serilog</summary>
        private static ILogger _logger;

        /// <summary>Settings</summary>
        public static Settings mySettings;

        /// <summary>Twitchlib Pubsub</summary>
        public static ITwitchPubSub PubSub;

        public static ITwitchAPI API;

        public static NamedPipeServerStream pipeServer;
        public static StreamWriter sw;
        /// <summary>
        /// 
        /// </summary>
        public Dictionary<int, CustomReward> rewards;

        public string KillPlayerString = "Among Us: Kill Player";
        public string SwapPlayersString = "Among Us: Swap Players";


        /*
        ~Program()
        {
            _logger.Information("Going in destructor");
            Task.Run(() => updateCustomReward(rewards[0].Id, rewards[0].Prompt, false));
            Task.Run(() => updateCustomReward(rewards[1].Id, rewards[1].Prompt, false));
        }
        */

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
            new Program()
                .MainAsync(args)
                .GetAwaiter()
                .GetResult();
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

            rewards = new Dictionary<int, CustomReward>();
            //set up twitchlib api
            API = new TwitchAPI();
            API.Settings.ClientId = clientId;
            API.Settings.Secret = secret;

            var twitchRewards = await getCustomRewards();
            for (int i = 0; i <= twitchRewards.Length - 1; i++)
            {
                int rewardKey = -1;
                if (twitchRewards[i].Title == KillPlayerString)
                {
                    rewardKey = 0;
                }
                else if (twitchRewards[i].Title == SwapPlayersString)
                {
                    rewardKey = 1;
                }
                if (rewardKey >= 0)
                {
                    _logger.Information($"{rewardKey} for {twitchRewards[i].Title}");
                    rewards.Add(rewardKey, await updateCustomReward(
                        twitchRewards[i].Id, twitchRewards[i].Prompt, true));
                }
            }

            // if rewards isn't set up, add it
            if (!rewards.ContainsKey(0))
            {
                rewards.Add(0, await createKillPlayerReward());
            }

            if (!rewards.ContainsKey(1))
            {
                rewards.Add(1, await createSwapPlayersReward());
            }

            //Set up twitchlib pubsub
            PubSub = new TwitchPubSub();
            PubSub.OnListenResponse += OnListenResponse;
            PubSub.OnPubSubServiceConnected += OnPubSubServiceConnected;
            PubSub.OnPubSubServiceClosed += OnPubSubServiceClosed;
            PubSub.OnPubSubServiceError += OnPubSubServiceError;


            //Set up listeners
            ListenToRewards(channelId);

            //Connect to pubsub
            PubSub.Connect();

            pipeServer = new NamedPipeServerStream("testpipe", PipeDirection.InOut, 4);
            sw = new StreamWriter(pipeServer);
            System.Console.WriteLine("NamedPipeServerStream object created.");

            // Wait for a client to connect
            System.Console.Write("Waiting for client connection...");
            await pipeServer.WaitForConnectionAsync();

            System.Console.WriteLine("Client connected.");

            //Keep the program going
            await Task.Delay(Timeout.Infinite);
        }

        #region Reward APIs

        /// <summary>
        /// Refreshes the token when the user access token expires. Updates the
        /// access token and the settings.json. See the Twitch documentation at
        /// https://dev.twitch.tv/docs/authentication#refreshing-access-tokens
        /// for more info.
        /// </summary>
        public async Task refreshAccessToken()
        {
            try
            {
                var response = await API.V5.Auth.RefreshAuthTokenAsync(
                    mySettings.twitch.token.refresh, mySettings.twitch.api.secret, mySettings.twitch.api.clientId);
                mySettings.twitch.token.userAccess = response.AccessToken;
            }
            catch (BadRequestException e)
            {
                _logger.Information($"{e.Message}");
            }
        }

        /// <summary>
        /// Get a list of custom rewards from the channel. See the Twitch API
        /// documentation at
        /// https://dev.twitch.tv/docs/api/reference#get-custom-reward
        /// for more info.
        /// </summary>
        public async Task<CustomReward[]> getCustomRewards()
        {
            try {
                var response = await API.Helix.ChannelPoints.GetCustomReward(
                    mySettings.twitch.channelId, null, false, mySettings.twitch.token.userAccess);
                return response.Data;
            }
            catch (InvalidCredentialException e)
            {
                _logger.Information($"{e.Message}");
                await refreshAccessToken();
                return await getCustomRewards();
            }
        }

        /// <summary>
        /// Creates the Kill Player custom reward on the channel using
        /// the CreateCustomRewards API.
        /// </summary>
        public Task<CustomReward> createKillPlayerReward()
        {
            var killReward = new CreateCustomRewardsRequest();
            killReward.Title = KillPlayerString;
            killReward.Cost = 10000;
            killReward.Prompt = "Name a player to kill: ";
            killReward.IsEnabled = true;
            killReward.IsUserInputRequired = true;
            killReward.IsGlobalCooldownEnabled = true;
            killReward.GlobalCooldownSeconds = 1000;
            return createCustomReward(killReward);
        }

        /// <summary>
        /// Creates the Swap Players custom reward created on the channel using
        /// the CreateCustomRewards API.
        /// </summary>
        public Task<CustomReward> createSwapPlayersReward()
        {
            var swapReward = new CreateCustomRewardsRequest();
            swapReward.Title = SwapPlayersString;
            swapReward.Cost = 10000;
            swapReward.Prompt = "Swap players with each other randomly";
            swapReward.IsEnabled = true;
            swapReward.IsUserInputRequired = false;
            swapReward.IsGlobalCooldownEnabled = true;
            swapReward.GlobalCooldownSeconds = 1000;
            return createCustomReward(swapReward);
        }

        /// <summary>
        /// Creates the custom reward if it does not exist already. 
        /// See the Twitch API documentation at
        /// https://dev.twitch.tv/docs/api/reference#create-custom-rewards
        /// for more info.
        /// </summary>
        public async Task<CustomReward> createCustomReward(CreateCustomRewardsRequest reward)
        {
            Console.WriteLine("Inside of createCustomReward");
            try
            {
                var response = await API.Helix.ChannelPoints.CreateCustomRewards(
                    mySettings.twitch.channelId, reward, mySettings.twitch.token.userAccess);
                _logger.Information($"Reward for ${response.Data[0].Title} has been added with the RewardId = {response.Data[0].Id}");
                return response.Data[0];
            }
            catch (InvalidCredentialException e)
            {
                _logger.Information($"{e.Message}");
                await refreshAccessToken();
                return await createCustomReward(reward);
            }
            catch (BadRequestException e)
            {
                _logger.Information($"{e.Message}");
                return null;
            }
        }

        /// <summary>
        /// Updates the custom reward created on the channel.
        /// See the Twitch API documentation at
        /// https://dev.twitch.tv/docs/api/reference#update-custom-reward
        /// for more info.
        /// </summary>
        public async Task<CustomReward> updateCustomReward(string rewardId, string prompt, bool isEnabled = false)
        {
            Console.WriteLine("Inside of updateCustomReward");
            try
            {
                var updateReward = new UpdateCustomRewardRequest();
                updateReward.Prompt = prompt;
                updateReward.IsEnabled = isEnabled;
                var response = await API.Helix.ChannelPoints.UpdateCustomReward(
                    mySettings.twitch.channelId, rewardId, updateReward, mySettings.twitch.token.userAccess);
                _logger.Information($"Reward {response.Data[0].Id} has been updated");
                return response.Data[0];
            }
            catch (InvalidCredentialException e)
            {
                _logger.Information($"{e.Message}");
                await refreshAccessToken();
                return await updateCustomReward(rewardId, prompt, isEnabled);
            }
            catch (BadRequestException e)
            {
                _logger.Information($"{e.Message}");
                return null;
            }
        }

        #endregion

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

        private void PubSub_OnRewardRedeemed(object sender, OnRewardRedeemedArgs e)
        {
            //Statuses can be:
            // "UNFULFILLED": when a user redeemed the reward
            // "FULFILLED": when a broadcaster or moderator marked the reward as complete
            if (e.Status == "UNFULFILLED")
            {
                _logger.Information($"{e.DisplayName} redeemed: {e.RewardTitle} " +
                    $"with prompt ${e.RewardPrompt.Split()}. With message: {e.Message}");
                fulfillCustomReward(e);
                API.Helix.ChannelPoints.UpdateCustomRewardRedemptionStatus(e.ChannelId, e.RewardId.ToString(),
                    new List<string>() { e.RedemptionId.ToString() }, 
                    new UpdateCustomRewardRedemptionStatusRequest() { Status = CustomRewardRedemptionStatus.CANCELED });
            }

            if (e.Status == "FULFILLED")
            {
                _logger.Information($"Reward from {e.DisplayName} ({e.RewardTitle}) has been marked as complete");
            }
        }

        private void fulfillCustomReward(OnRewardRedeemedArgs e)
        {
            if (e.RewardTitle == "Among Us: Kill Player")
            {
                if (isValidCustomReward(e.RewardPrompt, e.Message))
                {
                    _logger.Information($"This is a valid kill. Sending over to Among Us Mod");
                    // TODO: Pipe the command over to the C# app
                    // If the pipe returns success, call UpdateCustomRewardRedemptionStatus with status fulfilled
                    try
                    {
                        // Read user input and send that to the client process.
                        sw.AutoFlush = true;
                        System.Console.Write("Enter text: ");
                        sw.WriteLine(Console.ReadLine());
                        sw.WriteLine(Console.ReadLine());
                        sw.WriteLine("deeznuts");
                        pipeServer.WaitForPipeDrain();
                    }
                    // Catch the IOException that is raised if the pipe is broken
                    // or disconnected.
                    catch (IOException ex)
                    {
                        System.Console.WriteLine("ERROR: {0}", ex.Message);
                    }
                }
            }
            else if (e.RewardTitle == "Among Us: Swap Players")
            {
                _logger.Information($"Going to swap players");
                // TODO: Pipe the command over to the C# app
                // If the pipe returns success, call UpdateCustomRewardRedemptionStatus with status fulfilled
            }
        }

        private bool isValidCustomReward(string rewardPrompt, string rewardMessage)
        {
            bool isValid = true;
            var prompt = rewardPrompt.Split('\n');
            for(int i = 1; i <= prompt.Length - 1; i++)
            {
                _logger.Information($"{i}: {prompt[i]}");
                if (prompt[i] == rewardMessage)
                {
                    isValid = true;
                }
            }
            return isValid;
        }

        private async void server()
        {
            // var pipeServer = new NamedPipeServerStream("testpipe", PipeDirection.InOut, 4);

            StreamReader sr = new StreamReader(pipeServer);
            StreamWriter sw = new StreamWriter(pipeServer);

            do
            {
                try
                {
                    pipeServer.WaitForConnection();
                    string test;
                    sw.WriteLine("Waiting");
                    sw.Flush();
                    pipeServer.WaitForPipeDrain();
                    test = await sr.ReadLineAsync();
                    Console.WriteLine(test);
                }

                catch (Exception ex) { throw ex; }

                finally
                {
                    pipeServer.WaitForPipeDrain();
                    // if (pipeServer.IsConnected) { pipeServer.Disconnect(); }
                }
            } while (true);
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