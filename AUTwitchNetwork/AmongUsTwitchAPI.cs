using System;
using System.Collections.Generic;
using System.Diagnostics;
using Serilog;
using System.Threading.Tasks;
using System.Net.Http;
using Windows.ApplicationModel.Resources;
using Windows.Security.Authentication.Web;
using Windows.UI.Core;
using TwitchLib.Api;
using TwitchLib.Api.Core.Enums;
using TwitchLib.Api.Core.Exceptions;
using TwitchLib.Api.Helix.Models.ChannelPoints;
using TwitchLib.Api.Helix.Models.ChannelPoints.CreateCustomReward;
using TwitchLib.Api.Helix.Models.ChannelPoints.UpdateCustomReward;
using TwitchLib.Api.Helix.Models.ChannelPoints.UpdateCustomRewardRedemptionStatus;
using TwitchLib.Api.Interfaces;
using TwitchLib.PubSub.Events;
using System.Threading;

namespace AmongUsTwitchNetwork
{
    public class AmongUsTwitchAPI
    {
        private static ILogger _logger;
        public static ITwitchAPI API;
        protected HttpClient client;
        protected string channelId;
        protected string clientId;
        protected string redirectURI;
        protected string refreshToken;
        protected string accessToken;

        /// <summary>
        /// Dictionary of the custom rewards
        /// </summary>
        public Dictionary<int, CustomReward> rewards;

        public AmongUsTwitchAPI(Settings settings)
        {
            client = new HttpClient();
            var outputTemplate = "[{Timestamp:HH:mm:ss} {Level}] {Message}{NewLine}{Exception}";
            _logger = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .WriteTo.Console(outputTemplate: outputTemplate)
                .WriteTo.File("log/api_log_.txt", outputTemplate: outputTemplate, rollingInterval: RollingInterval.Day)
                .CreateLogger();

            rewards = new Dictionary<int, CustomReward>();

            API = new TwitchAPI();

            this.channelId = settings.twitch.channelId;
            this.clientId = "68nrjzl7gsxwkxhjf0bbyva2go57ty";
            this.redirectURI = "https://theramik.github.io/Among-Us-Twitch-Mod/AUTwitchNetwork/";
            this.accessToken = settings.twitch.token.userAccess;
            this.refreshToken = settings.twitch.token.refresh;
        }

        ~AmongUsTwitchAPI()
        {
            _logger.Information($"In deconstructor");
            Task.Run(() => DisableAmongUsTwitchRewards());
            Thread.Sleep(1000);
        }

        public ITwitchAPI getAPI()
        {
            return API;
        }

        #region Reward APIs

        /// <summary>
        /// Gets all the custom rewards and checks if they exist already. If it does,
        /// retrieve it and store it into a dictionary.
        /// </summary>
        public async Task DisableAmongUsTwitchRewards()
        {
            for (int i = 0; i <= rewards.Count - 1; i++)
            {
                await UpdateCustomRewardAsync(rewards[i].Id, rewards[i].Prompt, false);
            }
        }

        /// <summary>
        /// Gets all the custom rewards and checks if they exist already. If it does,
        /// retrieve it and store it into a dictionary.
        /// </summary>
        public async Task CreateAmongUsTwitchRewards()
        {
            var twitchRewards = await GetCustomRewardsAsync();
            for (int i = 0; i <= twitchRewards.Length - 1; i++)
            {
                int rewardKey = -1;
                bool enableReward = true;
                if (twitchRewards[i].Title == Constants.KillPlayerString)
                {
                    rewardKey = (int)Constants.Rewards.KillPlayer;
                    enableReward = false;
                }
                else if (twitchRewards[i].Title == Constants.SwapPlayersString)
                {
                    rewardKey = (int)Constants.Rewards.KillRandomPlayer;
                }
                else if (twitchRewards[i].Title == Constants.KillRandomPlayerString)
                {
                    rewardKey = (int)Constants.Rewards.SwapPlayer;
                }
                if (rewardKey >= 0)
                {
                    _logger.Information($"{rewardKey} for {twitchRewards[i].Title}");
                    rewards.Add(rewardKey, await UpdateCustomRewardAsync(
                        twitchRewards[i].Id, twitchRewards[i].Prompt, enableReward));
                }
            }

            // if rewards isn't set up, add it
            if (!rewards.ContainsKey((int)Constants.Rewards.KillPlayer))
            {
                rewards.Add((int)Constants.Rewards.KillPlayer, await CreateKillPlayerReward());
            }

            if (!rewards.ContainsKey((int)Constants.Rewards.KillRandomPlayer))
            {
                rewards.Add((int)Constants.Rewards.KillRandomPlayer, await CreateRandomKillPlayerReward());
            }

            if (!rewards.ContainsKey((int)Constants.Rewards.SwapPlayer))
            {
                rewards.Add((int)Constants.Rewards.SwapPlayer, await CreateSwapPlayersReward());
            }
        }

        public async Task<string> AuthorizeTwitchAsync()
        {
            try
            {
                var uri = "https://id.twitch.tv/oauth2/authorize";
                uri += "?client_id=" + clientId;
                uri += "&redirect_uri=" + redirectURI;
                uri += "&response_type=token";
                uri += "&scope=channel:manage:redemptions";
                var endUri = new Uri(redirectURI);
                WebAuthenticationResult WebAuthenticationResult = await WebAuthenticationBroker.AuthenticateAsync(WebAuthenticationOptions.None, uri, endUri);
                // var content = new StringContent("application/x-www-form-urlencoded");
                // var request = new HttpRequestMessage(new HttpMethod("GET"), uri);
                Process.Start(uri);
                // List<AuthScopes> authScopeList = new List<AuthScopes>();
                // authScopeList.Add(AuthScopes.Helix_Channel_Manage_Redemptions);
                // var response = API.V5.Auth.GetAuthorizationCodeUrl(redirectURI, authScopeList, true, null, clientId);
                // var response = await client.SendAsync(request);
                // _logger.Information("response:" + response);
                /*
                var response = await client.PostAsync(uri, content);
                if (response != null)
                {
                    var results = await responseJsonToObject<RefreshResponse>(response.Content);
                    mySettings.twitch.token.userAccess = results.access_token;
                }
                var response = await API.V5.Auth.GetAccessTokenFromCodeAsync(
                    this.refreshToken, this.secret, this.clientId);
                this.accessToken = response.AccessToken;
                */
                _logger.Information("request");
                return "finished";
            }
            catch (Exception e)
            {
                _logger.Error($"{e.Message}");
                throw e;
            }
            // public Task<AuthCodeResponse> GetAccessTokenFromCodeAsync(string code, string clientSecret, string redirectUri, string clientId = null);
        }

        /// <summary>
        /// Converts the JSON from the HttpContent to a object based on the
        /// object type we pass in.
        /// </summary>
        // private async Task<T> responseJsonToObject<T>(HttpContent content)
        // {
        //     var jsonString = await content.ReadAsStringAsync();
        //     return JsonConvert.DeserializeObject<T>(jsonString);
        // }

        /// <summary>
        /// Refreshes the token when the user access token expires. Updates the
        /// access token and the settings.json. See the Twitch documentation at
        /// https://dev.twitch.tv/docs/authentication#refreshing-access-tokens
        /// for more info.
        /// </summary>
        public async Task<string> RefreshAccessTokenAsync()
        {
            try
            {
                // var response = await API.V5.Auth.RefreshAuthTokenAsync(
                //     this.refreshToken, this.secret, this.clientId);
                // this.accessToken = response.AccessToken;
            }
            catch (Exception e)
            {
                _logger.Error($"{e.Message}");
                throw e;
            }
            return this.accessToken;
        }

        /// <summary>
        /// Get a list of custom rewards from the channel. See the Twitch API
        /// documentation at
        /// https://dev.twitch.tv/docs/api/reference#get-custom-reward
        /// for more info.
        /// </summary>
        public async Task<CustomReward[]> GetCustomRewardsAsync()
        {
            try
            {
                var response = await API.Helix.ChannelPoints.GetCustomReward(
                    this.channelId, null, false, this.accessToken);
                return response.Data;
            }
            catch (InvalidCredentialException e)
            {
                _logger.Error($"{e.Message}");
                await RefreshAccessTokenAsync();
                return await GetCustomRewardsAsync();
            }
            catch (Exception e)
            {
                _logger.Error($"{e.Message}");
                throw e;
            }
        }

        /// <summary>
        /// Creates the Kill Player custom reward on the channel using
        /// the CreateCustomRewards API.
        /// </summary>
        public Task<CustomReward> CreateKillPlayerReward()
        {
            var killReward = new CreateCustomRewardsRequest();
            killReward.Title = Constants.KillPlayerString;
            killReward.Cost = 1000;
            killReward.Prompt = "Name a player to kill: ";
            killReward.IsEnabled = false;
            killReward.IsUserInputRequired = true;
            killReward.IsGlobalCooldownEnabled = true;
            killReward.GlobalCooldownSeconds = 10;
            return CreateCustomReward(killReward);
        }

        /// <summary>
        /// Creates the Kill Random Player custom reward on the channel using
        /// the CreateCustomRewards API.
        /// </summary>
        public Task<CustomReward> CreateRandomKillPlayerReward()
        {
            var randomKillReward = new CreateCustomRewardsRequest();
            randomKillReward.Title = Constants.KillRandomPlayerString;
            randomKillReward.Cost = 1000;
            randomKillReward.Prompt = "Kills a Random Player";
            randomKillReward.IsEnabled = true;
            randomKillReward.IsUserInputRequired = false;
            randomKillReward.IsGlobalCooldownEnabled = true;
            randomKillReward.GlobalCooldownSeconds = 60;
            return CreateCustomReward(randomKillReward);
        }

        /// <summary>
        /// Creates the Swap Players custom reward created on the channel using
        /// the CreateCustomRewards API.
        /// </summary>
        public Task<CustomReward> CreateSwapPlayersReward()
        {
            var swapReward = new CreateCustomRewardsRequest();
            swapReward.Title = Constants.SwapPlayersString;
            swapReward.Cost = 500;
            swapReward.Prompt = "Swap players with each other randomly";
            swapReward.IsEnabled = true;
            swapReward.IsUserInputRequired = false;
            swapReward.IsGlobalCooldownEnabled = true;
            swapReward.GlobalCooldownSeconds = 10;
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
            try
            {
                var response = await API.Helix.ChannelPoints.CreateCustomRewards(
                    this.channelId, reward, this.accessToken);
                _logger.Information($"Reward for ${response.Data[0].Title} has been added with the RewardId = {response.Data[0].Id}");
                return response.Data[0];
            }
            catch (InvalidCredentialException e)
            {
                _logger.Information($"{e.Message}");
                await RefreshAccessTokenAsync();
                return await CreateCustomReward(reward);
            }
            catch (Exception e)
            {
                _logger.Error($"{e.Message}");
                throw e;
            }
        }

        /// <summary>
        /// Updates the custom reward created on the channel.
        /// See the Twitch API documentation at
        /// https://dev.twitch.tv/docs/api/reference#update-custom-reward
        /// for more info.
        /// </summary>
        public async Task<CustomReward> UpdateCustomRewardAsync(string rewardId, string prompt, bool isEnabled = false)
        {
            try
            {
                var updateReward = new UpdateCustomRewardRequest();
                updateReward.Prompt = prompt;
                updateReward.IsEnabled = isEnabled;
                var response = await API.Helix.ChannelPoints.UpdateCustomReward(
                    this.channelId, rewardId, updateReward, this.accessToken);
                _logger.Information($"Reward {response.Data[0].Id} has been updated");
                return response.Data[0];
            }
            catch (InvalidCredentialException e)
            {
                _logger.Information($"{e.Message}");
                await RefreshAccessTokenAsync();
                return await UpdateCustomRewardAsync(rewardId, prompt, isEnabled);
            }
            catch (Exception e)
            {
                _logger.Error($"{e.Message}");
                throw e;
            }
        }

        public async Task<RewardRedemption> UpdateRedemptionReward(OnRewardRedeemedArgs events, CustomRewardRedemptionStatus status)
        {
            try
            {
                var response = await API.Helix.ChannelPoints.UpdateCustomRewardRedemptionStatus(events.ChannelId, events.RewardId.ToString(),
                    new List<string>() { events.RedemptionId.ToString() },
                    new UpdateCustomRewardRedemptionStatusRequest() { Status = status },
                    this.accessToken);
                _logger.Information($"{response.Data[0]}");
                return response.Data[0];
            }
            catch (InvalidCredentialException e)
            {
                _logger.Information($"{e.Message}");
                await RefreshAccessTokenAsync();
                return await UpdateRedemptionReward(events, status);
            }
            catch (Exception e)
            {
                _logger.Error($"{e.Message}");
                throw e;
            }

        }

        #endregion


    }
}
