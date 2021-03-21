using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Serilog;
using TwitchLib.Api;
using TwitchLib.Api.Core.Enums;
using TwitchLib.Api.Helix.Models.ChannelPoints.UpdateCustomRewardRedemptionStatus;
using TwitchLib.Api.Interfaces;
using TwitchLib.PubSub;
using TwitchLib.PubSub.Enums;
using TwitchLib.PubSub.Events;
using TwitchLib.PubSub.Interfaces;
using TwitchLib.PubSub.Models;

/*
void LoadJson()
{
    using (StreamReader r = new StreamReader("Settings.json"))
    {
        string json = r.ReadToEnd();
        Console.WriteLine(json);
        // List<Item> items = JsonConvert.DeserializeObject<List<Item>>(json);
    }
}
*/

namespace ExampleTwitchPubsub
{
    public struct Settings
    {
        public Twitch twitch;
    }

    public struct Twitch
    {
        public string channelName;
        public string channelId;
        public SettingAPI api;
        public PubSub pubsub;
        public List<string> rewards;

    }

    public struct SettingAPI
    {
        public string clientId;
        public string secret;
    }

    public struct PubSub
    {
        public string oauth;
        public string refresh;
    }

    public struct UserData
    {
        public UserData(string channelId, string clientId, string secret, string accessToken)
        {
            ChannelId = channelId;
            ClientId = clientId;
            Secret = secret; 
            UserAccessToken = accessToken;
        }

        public string ChannelId { get; }
        public string ClientId { get; }
        public string Secret { get; }
        public string UserAccessToken { get; }

    }

    class CustomRewardBody
    {
        public string title { get; set; }
        public string prompt { get; set; }
        public int cost { get; set; }
        public bool is_enabled { get; set; }
        public bool is_global_countdown_enabled { get; set; }
        public int global_countdown_seconds { get; set; }
    }

    /// <summary>
    /// Represents the example bot
    /// </summary>
    public class Program
    {
        /// <summary>Serilog</summary>
        private static ILogger _logger;
        /// <summary>Settings</summary>
        public static IConfiguration Settings;

        public static Settings mySettings;
        /// <summary>Twitchlib Pubsub</summary>
        public static ITwitchPubSub PubSub;

        public static ITwitchAPI API;

        static HttpClient client;

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

            Settings = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("Settings.json", false, true)
                .AddEnvironmentVariables()
                .Build();

            using (StreamReader r = new StreamReader("Settings.json"))
            {
                string json = r.ReadToEnd();
                Console.WriteLine(json);
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
            // var channelId = Settings.GetSection("twitch").GetValue<string>("channelId");
            var channelId = mySettings.twitch.channelId; //"79351610";
            var clientId = mySettings.twitch.api.clientId;//"68nrjzl7gsxwkxhjf0bbyva2go57ty";// Settings.GetSection("twitch.api").GetValue<string>("client-id");
            var secret = mySettings.twitch.api.secret; // Settings.GetSection("twitch.api").GetValue<string>("secret");
            var accessToken = mySettings.twitch.pubsub.oauth; // Settings.GetSection("twitch.pubsub").GetValue<string>("oauth");
            client = new HttpClient();
            var userData = new UserData(channelId, clientId, secret, accessToken);

            Console.WriteLine("channelId: " + userData.ChannelId);
            Console.WriteLine("clientId: " + userData.ClientId);
            Console.WriteLine("secret: " + userData.Secret);
            Console.WriteLine("accessToken: " + userData.UserAccessToken);

            //set up twitchlib api
            API = new TwitchAPI();
            API.Settings.ClientId = clientId;
            API.Settings.Secret = secret;

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

            // createCustomReward
            CustomRewardBody testReward = new CustomRewardBody();
            testReward.title = "rickelz test";
            testReward.cost = 10000;

            createCustomReward(client, userData, testReward);

            // updateCustomReward
            updateCustomReward(client, userData, "e302729f-fcdc-4836-897f-54a57550bc83", "test test 1234");

            //Keep the program going
            await Task.Delay(Timeout.Infinite);
        }

        /// <summary>
        /// Refreshes the token when the user access token expires. Updates the
        /// access token and the settings.json.
        /// </summary>
        private void refreshAccessToken(HttpClient client, UserData userData)
        {

        }


        /// <summary>
        /// Creates the custom reward if it does not exist already. Appends the
        /// rewards's id to settings.json. See https://dev.twitch.tv/docs/api/reference#create-custom-rewards
        /// for more info.
        /// </summary>
        private async void createCustomReward(HttpClient client, UserData userData, CustomRewardBody rewardBody)
        {
            Console.WriteLine("Inside of createCustomReward");
            using (var request = new HttpRequestMessage(new HttpMethod("POST"), "https://api.twitch.tv/helix/channel_points/custom_rewards?broadcaster_id=" + userData.ChannelId))
            {
                request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + userData.UserAccessToken);
                request.Headers.TryAddWithoutValidation("client-id", userData.ClientId);

                request.Content = new StringContent(JsonConvert.SerializeObject(rewardBody));
                request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/json");
                Console.WriteLine(request.ToString());
                var response = await client.SendAsync(request);
                Console.WriteLine(response.ToString());
            }
        }

        /// <summary>
        /// Updates the custom reward created on the channel.
        /// See https://dev.twitch.tv/docs/api/reference#update-custom-reward for more info.
        /// </summary>
        private async void updateCustomReward(HttpClient client, UserData userData, string rewardId, string prompt)
        {
            Console.WriteLine("Inside of updateCustomReward");
            using (var request = new HttpRequestMessage(new HttpMethod("PATCH"), "https://api.twitch.tv/helix/channel_points/custom_rewards?broadcaster_id=" + userData.ChannelId + "&id=" + rewardId))
            {
                request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + userData.UserAccessToken);
                request.Headers.TryAddWithoutValidation("client-id", userData.ClientId);

                request.Content = new StringContent("{\"prompt\":\"" + prompt + "\"}");
                request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/json");
                Console.WriteLine(request.ToString());
                var response = await client.SendAsync(request);
                Console.WriteLine(response.ToString());
            }
        }

        #region Whisper Events

        private void ListenToWhispers(string channelId)
        {
            PubSub.OnWhisper += PubSub_OnWhisper;
            PubSub.ListenToWhispers(channelId);
        }

        private void PubSub_OnWhisper(object sender, OnWhisperArgs e)
        {
            // _logger.Information($"{e.Whisper.DataObjectWhisperReceived.Recipient.DisplayName} send a whisper {e.Whisper.DataObjectWhisperReceived.Body}");
        }

        #endregion

        #region Video Playback Events

        private void ListenToVideoPlayback(string channelId)
        {
            PubSub.OnStreamUp += PubSub_OnStreamUp;
            PubSub.OnStreamDown += PubSub_OnStreamDown;
            PubSub.OnViewCount += PubSub_OnViewCount;
            PubSub.ListenToVideoPlayback(channelId);
        }


        private void PubSub_OnViewCount(object sender, OnViewCountArgs e)
        {
            // _logger.Information($"Current viewers: {e.Viewers}");
        }

        private void PubSub_OnStreamDown(object sender, OnStreamDownArgs e)
        {
            // _logger.Information($"The stream is down");
        }

        private void PubSub_OnStreamUp(object sender, OnStreamUpArgs e)
        {
            // _logger.Information($"The stream is up");
        }

        #endregion

        #region Subscription Events

        private void ListenToSubscriptions(string channelId)
        {
            PubSub.OnChannelSubscription += PubSub_OnChannelSubscription;
            PubSub.ListenToSubscriptions(channelId);
        }

        private void PubSub_OnChannelSubscription(object sender, OnChannelSubscriptionArgs e)
        {
            var gifted = e.Subscription.IsGift ?? false;
            if (gifted)
            {
                // _logger.Information($"{e.Subscription.DisplayName} gifted a subscription to {e.Subscription.RecipientName}");
            }
            else
            {
                var cumulativeMonths = e.Subscription.CumulativeMonths ?? 0;
                if (cumulativeMonths != 0)
                {
                    // _logger.Information($"{e.Subscription.DisplayName} just subscribed (total of {cumulativeMonths} months)");
                }
                else
                {
                    // _logger.Information($"{e.Subscription.DisplayName} just subscribed");
                }

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
            _logger.Debug($"{e.RewardTitle} (\"{e.RewardId}\")");
        }

        private void PubSub_OnRewardRedeemed(object sender, OnRewardRedeemedArgs e)
        {
            //Statuses can be:
            // "UNFULFILLED": when a user redeemed the reward
            // "FULFILLED": when a broadcaster or moderator marked the reward as complete
            if (e.Status == "UNFULFILLED")
            {

                // _logger.Information($"{e.DisplayName} redeemed: {e.RewardTitle}");
                API.Helix.ChannelPoints.UpdateCustomRewardRedemptionStatus(e.ChannelId, e.RewardId.ToString(),
                    new List<string>() { e.RedemptionId.ToString() }, new UpdateCustomRewardRedemptionStatusRequest() { Status = CustomRewardRedemptionStatus.CANCELED });
            }

            if (e.Status == "FULFILLED")
            {
                // _logger.Information($"Reward from {e.DisplayName} ({e.RewardTitle}) has been marked as complete");
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
            var oauth = mySettings.twitch.pubsub.oauth;// Settings.GetSection("twitch.pubsub").GetValue<string>("oauth");
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