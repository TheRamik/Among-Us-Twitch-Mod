using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
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
using TwitchLib.PubSub.Events;
using TwitchLib.PubSub.Interfaces;

public class CustomRewardBody
{
    public string title { get; set; }
    public string prompt { get; set; }
    public int cost { get; set; }
    public bool is_user_input_required { get; set; }
    public bool is_enabled { get; set; }
    public bool is_global_countdown_enabled { get; set; }
    public int global_countdown_seconds { get; set; }
}

public struct RewardData
{
    public CustomRewardBody[] data;
}

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
    public List<string> rewards;

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

namespace ExampleTwitchPubsub
{

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
            var channelId = mySettings.twitch.channelId;
            var clientId = mySettings.twitch.api.clientId;
            var secret = mySettings.twitch.api.secret;
            var accessToken = mySettings.twitch.token.userAccess;
            client = new HttpClient();

            Console.WriteLine("channelId: " + channelId);
            Console.WriteLine("clientId: " + clientId);
            Console.WriteLine("secret: " + secret);
            Console.WriteLine("accessToken: " + accessToken);

            var rewards = await getCustomRewards();
            for (int i = 0; i < rewards.Length - 1; i++)
            {
                if (rewards[i].title == "Among Us: Kill Player" ||
                    rewards[i].title == "Among Us: Swap Players")
                {
                    Console.WriteLine("This exists");
                    Console.WriteLine("reward = " + rewards); 
                }
            }
            // Check if the access token is still valid; if not refresh
            // await refreshAccessToken(client);

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

            // updateCustomReward
            updateCustomReward("e302729f-fcdc-4836-897f-54a57550bc83", "test test 12345");

            //Keep the program going
            await Task.Delay(Timeout.Infinite);
        }

        #region Reward APIs

        /// <summary>
        /// Get a list of custom rewards from the channel. See the Twitch API
        /// documentation at
        /// https://dev.twitch.tv/docs/api/reference#get-custom-reward
        /// for more info.
        /// </summary>
        public async Task<CustomRewardBody[]> getCustomRewards()
        {
            Console.WriteLine("Inside of getCustomRewards");
            var request = createDefaultRequestMessage("GET", createCustomRewardURI());
            Console.WriteLine(request.ToString());
            var response = await client.SendAsync(request);
            Console.WriteLine(response.ToString());
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                await refreshAccessToken();
                request = createDefaultRequestMessage("GET", createCustomRewardURI());
                response = await client.SendAsync(request);
            }
            var results = await responseJsonToObject<RewardData>(response.Content);
            return results.data;
        }

        /// <summary>
        /// Refreshes the token when the user access token expires. Updates the
        /// access token and the settings.json. See the Twitch documentation at
        /// https://dev.twitch.tv/docs/authentication#refreshing-access-tokens
        /// for more info.
        /// </summary>
        public async Task refreshAccessToken()
        {
            var uri = "https://id.twitch.tv/oauth2/token";
            uri += "?grant_type=refresh_token";
            uri += "&refresh_token=" + mySettings.twitch.token.refresh;
            uri += "&client_id=" + mySettings.twitch.api.clientId;
            uri += "&client_secret=" + mySettings.twitch.api.secret;
            var content = new StringContent("application/x-www-form-urlencoded");
            var response = await client.PostAsync(uri, content);
            if (response != null)
            {
                var results = await responseJsonToObject<RefreshResponse>(response.Content);
                mySettings.twitch.token.userAccess = results.access_token;
            }

        }

        /// <summary>
        /// Creates the custom reward if it does not exist already. Appends the
        /// rewards's id to settings.json. See the Twitch API documentation at
        /// https://dev.twitch.tv/docs/api/reference#create-custom-rewards
        /// for more info.
        /// </summary>
        public async void createCustomReward(CustomRewardBody rewardBody)
        {
            Console.WriteLine("Inside of createCustomReward");
            Console.WriteLine(JsonConvert.SerializeObject(rewardBody));
            using (var request = createDefaultRequestMessage("POST", createCustomRewardURI()))
            {
                request.Content = new StringContent(JsonConvert.SerializeObject(rewardBody));
                request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/json");
                Console.WriteLine(request.ToString());
                var response = await client.SendAsync(request);
                Console.WriteLine(response.ToString());
            }
        }

        /// <summary>
        /// Updates the Kill Player custom reward created on the channel using
        /// the createCustomReward function.
        /// </summary>
        public void createKillPlayerReward()
        {
            CustomRewardBody killReward = new CustomRewardBody();
            killReward.title = "Among Us: Kill Player";
            killReward.cost = 10000;
            killReward.prompt = "Kill a specific player in game";
            killReward.is_enabled = true;
            killReward.is_user_input_required = true;
            killReward.is_global_countdown_enabled = true;
            killReward.global_countdown_seconds = 1000;
            createCustomReward(killReward);
        }

        /// <summary>
        /// Updates the custom reward created on the channel.
        /// See the Twitch API documentation at
        /// https://dev.twitch.tv/docs/api/reference#update-custom-reward
        /// for more info.
        /// </summary>
        public async void updateCustomReward(string rewardId, string prompt)
        {
            Console.WriteLine("Inside of updateCustomReward");
            using (var request = createDefaultRequestMessage("PATCH", createCustomRewardURI(rewardId)))
            {

                request.Content = new StringContent("{\"prompt\":\"" + prompt + "\"}");
                request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/json");
                Console.WriteLine(request.ToString());
                var response = await client.SendAsync(request);
                Console.WriteLine(response.ToString());
            }
        }

        // Private Methods

        /// <summary>
        /// Creates the custom reward URI. If the rewardId is provided it will
        /// add it to the URI.
        /// </summary>
        private string createCustomRewardURI(string rewardId = "")
        {
            string uri = "https://api.twitch.tv/helix/channel_points/custom_rewards?broadcaster_id=" + mySettings.twitch.channelId;
            if (!String.IsNullOrEmpty(rewardId))
            {
                uri += "&id=" + rewardId;
            }
            return uri;
        }

        /// <summary>
        /// Creates the HttpRequestMessage object that uses the same headers
        /// </summary>
        private HttpRequestMessage createDefaultRequestMessage(string method, string uri)
        {
            Console.WriteLine("uri = " + uri);
            var request = new HttpRequestMessage(new HttpMethod(method), uri);
            request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + mySettings.twitch.token.userAccess);
            request.Headers.TryAddWithoutValidation("client-id", mySettings.twitch.api.clientId);
            return request;
        }

        /// <summary>
        /// Converts the JSON from the HttpContent to a object based on the
        /// object type we pass in.
        /// </summary>
        private async Task<T> responseJsonToObject<T>(HttpContent content)
        {
            var jsonString = await content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<T>(jsonString);
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

                _logger.Information($"{e.DisplayName} redeemed: {e.RewardTitle} with prompt ${e.RewardPrompt.Split()}. With message: {e.Message}");
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
            var oauth = mySettings.twitch.token.userAccess;// Settings.GetSection("twitch.pubsub").GetValue<string>("oauth");
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