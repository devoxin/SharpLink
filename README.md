# SharpLink
A [Lavalink](https://github.com/Frederikam/Lavalink) wrapper for Discord.Net!

[![NuGet Pre Release](https://img.shields.io/nuget/vpre/SharpLink.svg?style=flat-square)](https://www.nuget.org/packages/SharpLink/)

## `Getting Started`

- Follow [these instructions](https://github.com/Frederikam/Lavalink#server-configuration) to setup Lavalink.
- Once Lavalink is up and running follow the code example below to setup Sharplink.

```cs
DiscordSocketClient client = new DiscordSocketClient();
LavalinkManager lavalinkManager = new LavalinkManager(client, new LavalinkManagerConfig
{
    RESTHost = "localhost",
    RESTPort = 2333,
    WebSocketHost = "localhost",
    WebSocketPort = 80,
    Authorization = "YOUR_SECRET_AUTHORIZATION_KEY",
    TotalShards = 1 
});
```
*Notes:* 
> You don't have to pass a `LavalinkManagerConfig` since Sharplink uses the default config.

> Set `TotalShards` to the total amount of shards your bot uses. If you don't know what the `TotalShards` are for your bot, use `DiscordRestClient#GetRecommendedShardCountAsync()` or set it to `1` if your bot is in less than ~2500 guilds.

> Use only a single instance of `LavaLinkManager`. If possible add `LavalinkManager` to your DI (Dependency Injection).

Once a LavalinkManager is set up it will need to be started. It is recommended you put this in the ready event.

```csharp
client.Ready += async () =>
{
    await lavalinkManager.StartAsync();
}
```

From there you can connect to audio channels, play music, and do whatever else you wish to do. Here is an example to connect and play music on a voice channel.

```cs
// Get LavalinkPlayer for our Guild and if it's null then join a voice channel.
LavalinkPlayer player = lavalinkManager.GetPlayer(GUILD_ID) ?? await lavalinkManager.JoinAsync(VOICE_CHANNEL);

// Now that we have a player we can go ahead and grab a track and play it
LavalinkTrack track = await lavalinkManager.GetTrackAsync("IDENTIFIER");
await player.PlayAsync(track);
```

*Notes:* To get a track from **Youtube** use `GetTrackAsync($"ytsearch:Query")`. To get a track from **SoundCloud** use `GetTrackAsync($"scsearch:Query")`.
