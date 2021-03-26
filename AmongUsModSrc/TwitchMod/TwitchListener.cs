using System;
using System.Collections.Generic;
using System.Text;
using TwitchLib.Api;
using TwitchLib.PubSub;
using TwitchLib.PubSub.Events;
using TwitchLib.PubSub.Interfaces;
using TwitchLib.Api.Core.Enums;
using TwitchLib.Api.Core.Exceptions;
using TwitchLib.Api.Helix.Models.ChannelPoints;
using TwitchLib.Api.Helix.Models.ChannelPoints.CreateCustomReward;
using TwitchLib.Api.Helix.Models.ChannelPoints.UpdateCustomReward;
using TwitchLib.Api.Helix.Models.ChannelPoints.UpdateCustomRewardRedemptionStatus;
using TwitchLib.Api.Interfaces;

namespace TwitchMod
{
    class TwitchListener
    {

        /// <summary>Settings</summary>
        public static Settings mySettings;

        /// <summary>Twitchlib Pubsub</summary>
        public static ITwitchPubSub PubSub;

        public TwitchListener()
        {
            mySettings = AmongUsTwitchAPI.mySettings;
            //Set up twitchlib pubsub
            PubSub = new TwitchPubSub();
            PubSub.OnListenResponse += OnListenResponse;
            PubSub.OnPubSubServiceConnected += OnPubSubServiceConnected;
            PubSub.OnPubSubServiceClosed += OnPubSubServiceClosed;
            PubSub.OnPubSubServiceError += OnPubSubServiceError;
        }

        ~TwitchListener()
        {

        }

        #region Reward Events

        public void Connect()
        {
            PubSub.Connect();
        }

        public void ListenToRewards(string channelId)
        {
            PubSub.OnRewardRedeemed += PubSub_OnRewardRedeemed;
            PubSub.OnCustomRewardCreated += PubSub_OnCustomRewardCreated;
            PubSub.OnCustomRewardDeleted += PubSub_OnCustomRewardDeleted;
            PubSub.OnCustomRewardUpdated += PubSub_OnCustomRewardUpdated;
            PubSub.ListenToRewards(channelId);
        }

        private void PubSub_OnCustomRewardUpdated(object sender, OnCustomRewardUpdatedArgs e)
        {
            System.Console.WriteLine($"Reward {e.RewardTitle} has been updated");
        }

        private void PubSub_OnCustomRewardDeleted(object sender, OnCustomRewardDeletedArgs e)
        {
            System.Console.WriteLine($"Reward {e.RewardTitle} has been removed");
        }

        private void PubSub_OnCustomRewardCreated(object sender, OnCustomRewardCreatedArgs e)
        {
            System.Console.WriteLine($"{e.RewardTitle} has been created");
            System.Console.WriteLine($"{e.RewardTitle} (\"{e.RewardId}\")");
        }

        private void PubSub_OnRewardRedeemed(object sender, OnRewardRedeemedArgs e)
        {
            //Statuses can be:
            // "UNFULFILLED": when a user redeemed the reward
            // "FULFILLED": when a broadcaster or moderator marked the reward as complete
            if (e.Status == "UNFULFILLED")
            {
                System.Console.WriteLine($"{e.DisplayName} redeemed: {e.RewardTitle} " +
                    $"with prompt ${e.RewardPrompt.Split()}. With message: {e.Message}");
                fulfillCustomReward(e);
                AmongUsTwitchAPI.API.Helix.ChannelPoints.UpdateCustomRewardRedemptionStatus(e.ChannelId, e.RewardId.ToString(),
                    new List<string>() { e.RedemptionId.ToString() },
                    new UpdateCustomRewardRedemptionStatusRequest() { Status = CustomRewardRedemptionStatus.CANCELED });
            }

            if (e.Status == "FULFILLED")
            {
                System.Console.WriteLine($"Reward from {e.DisplayName} ({e.RewardTitle}) has been marked as complete");
            }
        }

        public void fulfillCustomReward(OnRewardRedeemedArgs e)
        {
            if (e.RewardTitle == "Among Us: Kill Player")
            {
                if (isValidCustomReward(e.RewardPrompt, e.Message))
                {
                    System.Console.WriteLine($"This is a valid kill. Sending over to Among Us Mod");
                    // TODO: Pipe the command over to the C# app
                    // If the pipe returns success, call UpdateCustomRewardRedemptionStatus with status fulfilled
                }

            }
            else if (e.RewardTitle == "Among Us: Swap Players")
            {
                System.Console.WriteLine($"Going to swap players");
                // TODO: Pipe the command over to the C# app
                // If the pipe returns success, call UpdateCustomRewardRedemptionStatus with status fulfilled
            }
        }

        private bool isValidCustomReward(string rewardPrompt, string rewardMessage)
        {
            bool isValid = false;
            var prompt = rewardPrompt.Split('\n');
            for (int i = 1; i <= prompt.Length - 1; i++)
            {
                System.Console.WriteLine($"{i}: {prompt[i]}");
                if (prompt[i] == rewardMessage)
                {
                    isValid = true;
                }
            }
            return isValid;
        }

        #endregion

        #region Pubsub events

        private void OnPubSubServiceError(object sender, OnPubSubServiceErrorArgs e)
        {
            System.Console.WriteLine($"{e.Exception.Message}");
        }

        private void OnPubSubServiceClosed(object sender, EventArgs e)
        {
            System.Console.WriteLine($"Connection closed to pubsub server");
        }

        private void OnPubSubServiceConnected(object sender, EventArgs e)
        {
            System.Console.WriteLine($"Connected to pubsub server");
            var oauth = mySettings.twitch.token.userAccess;
            PubSub.SendTopics(oauth);
        }

        private void OnListenResponse(object sender, OnListenResponseArgs e)
        {
            if (!e.Successful)
            {
                System.Console.WriteLine($"Failed to listen! Response{e.Response}");
            }
        }

        #endregion
    }

}
