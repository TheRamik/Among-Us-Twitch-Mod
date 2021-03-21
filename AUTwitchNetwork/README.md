# Among Us Twitch API Network
Deez nuts

## Set-up

### Twitch CLI
#### Install Twitch CLI
Go to https://github.com/twitchdev/twitch-cli to show how to download twitch CLI with the Scoop or Homebrew

#### Configure Twitch CLI
https://github.com/twitchdev/twitch-cli/blob/main/docs/configure.md

#### Generate a Token 
```
twitch token -u -s "channel:manage:redemptions user:edit:follows"
```

#### Create a Settings.json
Copy Settings.json and fill in the fields with the appropriate information

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
