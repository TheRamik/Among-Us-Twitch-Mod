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
        public static StreamReader sr;
        /// <summary>
        /// 
        /// </summary>
        public Dictionary<int, CustomReward> rewards;


        public enum Rewards { KillPlayer, KillRandomPlayer, SwapPlayer };
        public string KillPlayerString = "Among Us: Kill Player";
        public string KillRandomPlayerString = "Among Us: Kill a Random Player";
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

            var twitchRewards = await GetCustomRewards();
            for (int i = 0; i <= twitchRewards.Length - 1; i++)
            {
                int rewardKey = -1;
                if (twitchRewards[i].Title == KillPlayerString)
                {
                    rewardKey = (int) Rewards.KillPlayer;
                }
                else if (twitchRewards[i].Title == SwapPlayersString)
                {
                    rewardKey = (int) Rewards.KillRandomPlayer;
                }
                else if (twitchRewards[i].Title == KillRandomPlayerString)
                {
                    rewardKey = (int) Rewards.SwapPlayer;
                }
                if (rewardKey >= 0)
                {
                    _logger.Information($"{rewardKey} for {twitchRewards[i].Title}");
                    rewards.Add(rewardKey, await UpdateCustomReward(
                        twitchRewards[i].Id, twitchRewards[i].Prompt, true));
                }
            }

            // if rewards isn't set up, add it
            if (!rewards.ContainsKey((int)Rewards.KillPlayer))
            {
                rewards.Add((int) Rewards.KillPlayer, await CreateKillPlayerReward());
            }

            if (!rewards.ContainsKey((int)Rewards.KillRandomPlayer))
            {
                rewards.Add((int) Rewards.KillRandomPlayer, await CreateRandomKillPlayerReward());
            }

            if (!rewards.ContainsKey((int)Rewards.SwapPlayer))
            {
                rewards.Add((int)Rewards.SwapPlayer, await CreateSwapPlayersReward());
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
            sr = new StreamReader(pipeServer);
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
        public async Task RefreshAccessToken()
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
        public async Task<CustomReward[]> GetCustomRewards()
        {
            try {
                var response = await API.Helix.ChannelPoints.GetCustomReward(
                    mySettings.twitch.channelId, null, false, mySettings.twitch.token.userAccess);
                return response.Data;
            }
            catch (InvalidCredentialException e)
            {
                _logger.Information($"{e.Message}");
                await RefreshAccessToken();
                return await GetCustomRewards();
            }
        }

        /// <summary>
        /// Creates the Kill Player custom reward on the channel using
        /// the CreateCustomRewards API.
        /// </summary>
        public Task<CustomReward> CreateKillPlayerReward()
        {
            var killReward = new CreateCustomRewardsRequest();
            killReward.Title = KillPlayerString;
            killReward.Cost = 10000;
            killReward.Prompt = "Name a player to kill: ";
            killReward.IsEnabled = true;
            killReward.IsUserInputRequired = true;
            killReward.IsGlobalCooldownEnabled = true;
            killReward.GlobalCooldownSeconds = 1000;
            return CreateCustomReward(killReward);
        }

        /// <summary>
        /// Creates the Kill Player custom reward on the channel using
        /// the CreateCustomRewards API.
        /// </summary>
        public Task<CustomReward> CreateRandomKillPlayerReward()
        {
            var randomKillReward = new CreateCustomRewardsRequest();
            randomKillReward.Title = KillRandomPlayerString;
            randomKillReward.Cost = 10000;
            randomKillReward.Prompt = "Kills a Random Player ";
            randomKillReward.IsEnabled = true;
            randomKillReward.IsUserInputRequired = false;
            randomKillReward.IsGlobalCooldownEnabled = true;
            randomKillReward.GlobalCooldownSeconds = 1000;
            return CreateCustomReward(randomKillReward);
        }

        /// <summary>
        /// Creates the Swap Players custom reward created on the channel using
        /// the CreateCustomRewards API.
        /// </summary>
        public Task<CustomReward> CreateSwapPlayersReward()
        {
            var swapReward = new CreateCustomRewardsRequest();
            swapReward.Title = SwapPlayersString;
            swapReward.Cost = 10000;
            swapReward.Prompt = "Swap players with each other randomly";
            swapReward.IsEnabled = true;
            swapReward.IsUserInputRequired = false;
            swapReward.IsGlobalCooldownEnabled = true;
            swapReward.GlobalCooldownSeconds = 1000;
            return CreateCustomReward(swapReward);
        }

        /// <summary>
        /// Creates the custom reward if it does not exist already. 
        /// See the Twitch API documentation at
        /// https://dev.twitch.tv/docs/api/reference#create-custom-rewards
        /// for more info.
        /// </summary>
        public async Task<CustomReward> CreateCustomReward(CreateCustomRewardsRequest reward)
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
                await RefreshAccessToken();
                return await CreateCustomReward(reward);
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
        public async Task<CustomReward> UpdateCustomReward(string rewardId, string prompt, bool isEnabled = false)
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
                await RefreshAccessToken();
                return await UpdateCustomReward(rewardId, prompt, isEnabled);
            }
            catch (BadRequestException e)
            {
                _logger.Information($"{e.Message}");
                return null;
            }
        }

        private async Task<RewardRedemption> UpdateRedemptionReward(OnRewardRedeemedArgs events, CustomRewardRedemptionStatus status)
        {
            try
            {
                var response = await API.Helix.ChannelPoints.UpdateCustomRewardRedemptionStatus(events.ChannelId, events.RewardId.ToString(),
                    new List<string>() { events.RedemptionId.ToString() },
                    new UpdateCustomRewardRedemptionStatusRequest() { Status = status },
                    mySettings.twitch.token.userAccess);
                _logger.Information($"{response.Data[0]}");
                return response.Data[0];
            }
            catch (InvalidCredentialException e)
            {
                _logger.Information($"{e.Message}");
                await RefreshAccessToken();
                return await UpdateRedemptionReward(events, status);
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
            if (e.RewardTitle == KillPlayerString)
            {
                if (isValidCustomReward(e.RewardPrompt, e.Message))
                {
                    _logger.Information($"This is a valid kill. Sending over to Among Us Mod");
                    // TODO: Pipe the command over to the C# app
                    // If the pipe returns success, call UpdateCustomRewardRedemptionStatus with status fulfilled
                    SendToPipe("killplayer:" + e.RewardPrompt);
                    await UpdateRedemptionReward(e, CustomRewardRedemptionStatus.FULFILLED);
                }
                else
                {
                    _logger.Information($"This is not a valid kill. We are returning the points.");
                    await UpdateRedemptionReward(e, CustomRewardRedemptionStatus.CANCELED);
                }
            }
            else if (e.RewardTitle == KillRandomPlayerString)
            {
                _logger.Information($"Going to kill random player");
                // TODO: Pipe the command over to the C# app
                // If the pipe returns success, call UpdateCustomRewardRedemptionStatus with status fulfilled
                SendToPipe("killrandomplayer");
                await UpdateRedemptionReward(e, CustomRewardRedemptionStatus.FULFILLED);
            }
            else if (e.RewardTitle == SwapPlayersString)
            {
                _logger.Information($"Going to swap players");
                // TODO: Pipe the command over to the C# app
                // If the pipe returns success, call UpdateCustomRewardRedemptionStatus with status fulfilled
                SendToPipe("swapplayers");
                await UpdateRedemptionReward(e, CustomRewardRedemptionStatus.FULFILLED);
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
                System.Console.WriteLine("ERROR: {0}", ex.Message);
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