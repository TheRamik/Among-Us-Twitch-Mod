using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using TwitchLib.Api;
using TwitchLib.Api.Core.Exceptions;
using TwitchLib.Api.Helix.Models.ChannelPoints;
using TwitchLib.Api.Helix.Models.ChannelPoints.CreateCustomReward;
using TwitchLib.Api.Helix.Models.ChannelPoints.UpdateCustomReward;
using TwitchLib.Api.Interfaces;

namespace TwitchMod
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

    public static class AmongUsTwitchAPI
    {
        /// <summary>Settings</summary>
        public static Settings mySettings;

        public static ITwitchAPI API;
        /// <summary>
        /// 
        /// </summary>
        public static Dictionary<int, CustomReward> rewards;

        public static string KillPlayerString = "Among Us: Kill Player";
        public static string SwapPlayersString = "Among Us: Swap Players";

        private static string channelId;
        private static string clientId;
        private static string secret;
        private static string accessToken;

        public static void InitAmongUsTwitchAPI()
        {
            ModManager.WriteToConsole("BigPP");
            var localLowPath = Path.Combine(Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData) + "Low\\Innersloth\\Among Us\\", "Settings.json");
            ModManager.WriteToConsole("localLowPath: "+ localLowPath);
            using (StreamReader r = new StreamReader(localLowPath))
            {
                string json = r.ReadToEnd();
                mySettings = JsonConvert.DeserializeObject<Settings>(json);
            }
            channelId = mySettings.twitch.channelId;
            clientId = mySettings.twitch.api.clientId;
            secret = mySettings.twitch.api.secret;
            accessToken = mySettings.twitch.token.userAccess;
            ModManager.WriteToConsole("channelId: " + channelId);
            ModManager.WriteToConsole("clientId: " + clientId);
            ModManager.WriteToConsole("secret: " + secret);
            ModManager.WriteToConsole("accessToken: " + accessToken);
            System.Console.WriteLine("channelId: " + channelId);
            System.Console.WriteLine("clientId: " + clientId);
            System.Console.WriteLine("secret: " + secret);
            System.Console.WriteLine("accessToken: " + accessToken);
            rewards = new Dictionary<int, CustomReward>();

            //set up twitchlib api
            API = new TwitchAPI();
            API.Settings.ClientId = clientId;
            API.Settings.Secret = secret;
            System.Console.WriteLine("Big pp ");
            System.Console.WriteLine("Efren's Big pp ");

        }

        public static async void initializeRewards()
        {
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
                    System.Console.WriteLine($"{rewardKey} for {twitchRewards[i].Title}");
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
        }

        /// <summary>
        /// Refreshes the token when the user access token expires. Updates the
        /// access token and the settings.json. See the Twitch documentation at
        /// https://dev.twitch.tv/docs/authentication#refreshing-access-tokens
        /// for more info.
        /// </summary>
        public static async Task refreshAccessToken()
        {
            try
            {
                var response = await API.V5.Auth.RefreshAuthTokenAsync(
                    mySettings.twitch.token.refresh, mySettings.twitch.api.secret, mySettings.twitch.api.clientId);
                mySettings.twitch.token.userAccess = response.AccessToken;
            }
            catch (BadRequestException e)
            {
                System.Console.WriteLine($"{e.Message}");
            }
        }

        /// <summary>
        /// Get a list of custom rewards from the channel. See the Twitch API
        /// documentation at
        /// https://dev.twitch.tv/docs/api/reference#get-custom-reward
        /// for more info.
        /// </summary>
        public static async Task<CustomReward[]> getCustomRewards()
        {
            try
            {
                var response = await API.Helix.ChannelPoints.GetCustomReward(
                    mySettings.twitch.channelId, null, false, mySettings.twitch.token.userAccess);
                return response.Data;
            }
            catch (InvalidCredentialException e)
            {
                System.Console.WriteLine($"{e.Message}");
                await refreshAccessToken();
                return await getCustomRewards();
            }
        }

        /// <summary>
        /// Creates the Kill Player custom reward on the channel using
        /// the CreateCustomRewards API.
        /// </summary>
        public static Task<CustomReward> createKillPlayerReward()
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
        public static Task<CustomReward> createSwapPlayersReward()
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
        public static async Task<CustomReward> createCustomReward(CreateCustomRewardsRequest reward)
        {
            System.Console.WriteLine("Inside of createCustomReward");
            try
            {
                var response = await API.Helix.ChannelPoints.CreateCustomRewards(
                    mySettings.twitch.channelId, reward, mySettings.twitch.token.userAccess);
                System.Console.WriteLine($"Reward for ${response.Data[0].Title} has been added with the RewardId = {response.Data[0].Id}");
                return response.Data[0];
            }
            catch (InvalidCredentialException e)
            {
                System.Console.WriteLine($"{e.Message}");
                await refreshAccessToken();
                return await createCustomReward(reward);
            }
            catch (BadRequestException e)
            {
                System.Console.WriteLine($"{e.Message}");
                return null;
            }
        }

        /// <summary>
        /// Updates the custom reward created on the channel.
        /// See the Twitch API documentation at
        /// https://dev.twitch.tv/docs/api/reference#update-custom-reward
        /// for more info.
        /// </summary>
        public static async Task<CustomReward> updateCustomReward(string rewardId, string prompt, bool isEnabled = false)
        {
            System.Console.WriteLine("Inside of updateCustomReward");
            try
            {
                var updateReward = new UpdateCustomRewardRequest();
                updateReward.Prompt = prompt;
                updateReward.IsEnabled = isEnabled;
                var response = await API.Helix.ChannelPoints.UpdateCustomReward(
                    mySettings.twitch.channelId, rewardId, updateReward, mySettings.twitch.token.userAccess);
                System.Console.WriteLine($"Reward {response.Data[0].Id} has been updated");
                return response.Data[0];
            }
            catch (InvalidCredentialException e)
            {
                System.Console.WriteLine($"{e.Message}");
                await refreshAccessToken();
                return await updateCustomReward(rewardId, prompt, isEnabled);
            }
            catch (BadRequestException e)
            {
                System.Console.WriteLine($"{e.Message}");
                return null;
            }
        }
        public static string GetChannelId()
        {
            return channelId;
        }


    }

}
