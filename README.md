# SharpLink
A lavalink wrapper for Discord.Net!

[![NuGet Pre Release](https://img.shields.io/nuget/vpre/SharpLink.svg?style=flat-square)](https://www.nuget.org/packages/SharpLink/1.0.0-beta)

# Getting Started
Here is some example code to help get you started with SharpLink.

First you want to make sure you have the LavalinkManager set up.
```csharp
DiscordSocketClient client = new DiscordSocketClient();

// You must pass the socket client over to the manager for reference
LavalinkManager lavalinkManager = new LavalinkManager(client, new LavalinkManagerConfig()
{
    // You do not have to pass a config. Default config is as provided at https://github.com/Frederikam/Lavalink/blob/master/LavalinkServer/application.yml.example
    RESTHost = "localhost";
    RESTPort = 2333,
    WebSocketHost = "localhost",
    WebSocketPort = 80,
    Authorization = "YOUR_SECRET_AUTHORIZATION_KEY",
    TotalShards = 1 // Please set this to the total amount of shards your bot uses
});
```

Once a LavalinkManager is set up it will need to be started. It is recommended you put this in the ready event.

```csharp
client.Ready += async () =>
{
    await lavalinkManager.StartAsync();
}
```

From there you can connect to audio channels, play music, and do whatever else you wish to do. Here is an example to connect and play music on a voice channel.

```csharp
// First we check if the guild has a player
LavalinkPlayer player = lavalinkManager.GetPlayer(GUILD_ID);

// Looks like we don't have a player let's join the channel the user is in
if (player == null)
{
    player = await lavalinkManager.JoinAsync(VOICE_CHANNEL);
}

// Now that we have a player we can go ahead and grab a track and play it
LavalinkTrack track = await lavalinkManager.GetTrack("QUERY");
await player.PlayAsync(track);
```
