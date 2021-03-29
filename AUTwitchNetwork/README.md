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

###### Retrieve your Channel Id
Using Twitch CLI, you can run API commands. You'll need your channel id for the settings.json later.
To get your channel id, run the following command:
```
twitch api get users -q login=<username>
```
![image](https://user-images.githubusercontent.com/19583901/112812899-7a53aa00-9032-11eb-94b1-9eb50945a206.png)

### Installation 
Streamers will need to download the latest "AmongUsTwitchNetwork" zip folder found [here.](https://github.com/TheRamik/Among-Us-Twitch-Mod/releases)

#### Create a Settings.json
Once unzipped, find the `Settings.json.example` file. Copy/rename `Settings.json.example` to `Settings.json` and fill in the fields with the appropriate information
in the appropriate fields.
If you did not use Twitch CLI, the last thing you'd need is your channelId. Get the Chrome extension in `Settings.json`.
Save the file and double-click `AmongUsTwitchNetwork.exe`. If all is working, you should see the following:
![image](https://user-images.githubusercontent.com/19583901/112814125-cbb06900-9033-11eb-817e-c5d231f2911f.png)


### Contribute/Development

#### Install Visual Studio 2019/ Visual Studio Mac
Download and install [Microsoft Visual Studio](https://visualstudio.microsoft.com/downloads/)

#### Visual Studio Environment
After installing Visaul Studio, add the `ASP.NET and web development` component in the Workloads tab
![image](https://user-images.githubusercontent.com/19583901/112814797-804a8a80-9034-11eb-898f-2d871fa18418.png)

Ensure that you have the .Net Framework 4.7.2 installed.
Double-click on the solution `AUTwitchNetwork.sln` and it should open up.

#### NuGet Package Manager
The next steps to get the program working is to install all the libraries. We are using the NuGet Package Manager to help us install all the packages we need. 
Click on `Tools` on the top, then move your mouse to `NugetPackage Manager` and then click on `NuGet Package Manager Console` or `Manage Nuget Packages for Solution...`
![image](https://user-images.githubusercontent.com/19583901/112815428-28605380-9035-11eb-80ce-7c4ee847784e.png)


#### Install TwitchLib
Either run the following command in the NuGet Package Manager console or search for TwitchLib in the `Browse` section from the `Nuget - Solution` tab.
``` 
Install-Package TwitchLib
```

#### Install dependencies
Install the rest of the dependencies the same way as we did for `TwitchLib`.
```
Install-Package Serilog
Install-Package Newtonsoft.Json
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
