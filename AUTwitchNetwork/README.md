# Among Us Twitch API Network

## Getting Started
In order to get the Twitch API Network up and running, you'll first need to get your TwitchDev environment set up. 
To make an application that uses the Twitch API, you first need to [register your application on the Twitch developer site.](https://dev.twitch.tv/console/apps/create)
After you register your application, store the client-id and secret somewhere. You will need it to get the program working.

### Prerequisite
#### Generate a Token
You can either generate a token using the [Twitch Token Generator](https://twitchtokengenerator.com/) website or Twitch CLI.

##### Twitch Token Generator
When arriving to the site, a pop-up will appear at the top. Click on 'Custom Scope Token'.
![image](https://user-images.githubusercontent.com/19583901/112807188-7ae94200-902c-11eb-9b46-bb8db63db9bf.png)

Afterwards, scroll down to Helix section and choose 'Yes' on `channel:manage:redemptions`.
![image](https://user-images.githubusercontent.com/19583901/112807540-e3382380-902c-11eb-8ef8-a361cc8ff041.png)

Scroll near the bottom and you will see a generate token.
At the top of the page, you will now see newly generated client-id, access token, and refresh token.

##### Twitch CLI
###### Install Twitch CLI
Go to https://github.com/twitchdev/twitch-cli to show how to download twitch CLI with the Scoop or Homebrew

###### Configure Twitch CLI
Open a command prompt or terminal.
Take out your client-id and client-secret and run the following command:
```
twitch configure --client-id <client-id> --client-secret <client-secret>
```
For more info on [how to configure the Twitch CLI](https://github.com/twitchdev/twitch-cli/blob/main/docs/configure.md). 

###### Generate the Token
Now that your Twitch CLI is configured, you can easily generate your token with the following command:
```
twitch token -u -s "channel:manage:redemptions"
```
![image](https://user-images.githubusercontent.com/19583901/112809089-83db1300-902e-11eb-8ead-7639ecf8ca47.png)


#### Create a Settings.json
Copy Settings.json.example and fill in the fields with the appropriate information

## Installs

### Install Visual Studio 2019/ Visual Studio Mac
Go to 

### Install TwitchLib
``` 
Install-Package TwitchLib
```

### Install dependencies
```
Install-Package Microsoft.Extensions.Configuration.Json
Install-Package Microsoft.Extensions.Configuration.Binder
Install-Package Microsoft.Extensions.Configuration.EnvironmentVariables
Install-Package Microsoft.Extensions.Configuration.FileExtensions
```

### References:
https://github.com/JayJay1989/TwitchLib.Pubsub.Example
https://dev.twitch.tv/docs/authentication
https://github.com/twitchdev/twitch-cli
https://github.com/twitchdev/channel-points-node-sample
https://dev.twitch.tv/docs/api/reference#update-custom-reward
https://dev.twitch.tv/console
https://dev.twitch.tv/docs/authentication#refreshing-access-tokens
