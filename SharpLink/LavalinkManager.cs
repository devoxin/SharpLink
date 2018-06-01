using Discord;
using Discord.WebSocket;
using Newtonsoft.Json.Linq;
using SharpLink.Enums;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace SharpLink
{
    public class LavalinkManager
    {
        private LavalinkWebSocket webSocket;
        private LavalinkManagerConfig config;
        private DiscordSocketClient discordClient;
        private Dictionary<ulong, LavalinkPlayer> players = new Dictionary<ulong, LavalinkPlayer>();
        private int connectionWait = 3000;
        private CancellationTokenSource lavalinkCancellation;

        #region PUBLIC_EVENTS
        public event Func<LavalinkPlayer, LavalinkTrack, long, Task> PlayerUpdate;
        public event Func<LavalinkPlayer, LavalinkTrack, string, Task> PlayerEnd;
        public event Func<LavalinkPlayer, LavalinkTrack, long, Task> PlayerStuck;
        public event Func<LavalinkPlayer, LavalinkTrack, string, Task> PlayerException;
        #endregion

        /// <summary>
        /// Initiates a new Lavalink node connection
        /// </summary>
        /// <param name="discordClient"></param>
        /// <param name="config"></param>
        public LavalinkManager(DiscordSocketClient discordClient, LavalinkManagerConfig config = null)
        {
            this.config = config ?? new LavalinkManagerConfig();
            this.discordClient = discordClient;

            // Setup the socket client events
            discordClient.VoiceServerUpdated += async (voiceServer) =>
            {
                Console.WriteLine(new LogMessage(LogSeverity.Debug, "Lavalink", "VOICE_SERVER_UPDATE(" + voiceServer.Guild.Id + ")"));

                await players[voiceServer.Guild.Id]?.UpdateSessionAsync(SessionChange.Connect, voiceServer);
            };

            discordClient.UserVoiceStateUpdated += async (user, oldVoiceState, newVoiceState) =>
            {
                // We only need voice state updates for the current user
                if (user.Id == discordClient.CurrentUser.Id)
                {
                    if (oldVoiceState.VoiceChannel == null && newVoiceState.VoiceChannel != null)
                    {
                        Console.WriteLine(new LogMessage(LogSeverity.Debug, "Lavalink", "VOICE_STATE_UPDATE(" + newVoiceState.VoiceChannel.Guild.Id + ", Connected)"));

                        // Connected
                        LavalinkPlayer player = players[newVoiceState.VoiceChannel.Guild.Id];
                        player?.SetSessionId(newVoiceState.VoiceSessionId);
                    }
                    else if (oldVoiceState.VoiceChannel != null && newVoiceState.VoiceChannel == null)
                    {
                        Console.WriteLine(new LogMessage(LogSeverity.Debug, "Lavalink", "VOICE_STATE_UPDATE(" + oldVoiceState.VoiceChannel.Guild.Id + ", Disconnected)"));

                        // Disconnected
                        LavalinkPlayer player = players[oldVoiceState.VoiceChannel.Guild.Id];

                        if (player != null)
                        {
                            player.SetSessionId("");
                            await player.UpdateSessionAsync(SessionChange.Disconnect, oldVoiceState.VoiceChannel.Guild.Id);
                            players.Remove(oldVoiceState.VoiceChannel.Guild.Id);
                        }
                    }
                }
            };

            discordClient.Disconnected += (exception) =>
            {
                foreach(KeyValuePair<ulong, LavalinkPlayer> player in players)
                {
                    player.Value.DisconnectAsync().GetAwaiter();
                }

                players.Clear();

                return Task.CompletedTask;
            };
        }

        internal async Task PlayTrackAsync(string trackId, ulong guildId)
        {
            JObject data = new JObject();
            data.Add("op", "play");
            data.Add("guildId", guildId.ToString());
            data.Add("track", trackId);

            await webSocket.SendAsync(data.ToString());
        }

        internal DiscordSocketClient GetDiscordClient()
        {
            return discordClient;
        }

        internal LavalinkWebSocket GetWebSocket()
        {
            return webSocket;
        }

        private void ConnectWebSocket()
        {
            // Continuously attempt connections to Lavalink
            Task.Run(async () =>
            {
                while (!webSocket.IsConnected())
                {
                    if (lavalinkCancellation.IsCancellationRequested)
                        break;

                    try
                    {
                        await webSocket.Connect();
                    }
                    catch (Exception ex)
                    {
                        connectionWait = connectionWait + 3000;
                        Console.WriteLine(new LogMessage(LogSeverity.Info, "Lavalink", "Failed to connect to Lavalink node. Waiting " + connectionWait/1000 + " seconds", ex));
                    }

                    if (!webSocket.IsConnected())
                    {
                        connectionWait = connectionWait + 3000;
                        Console.WriteLine(new LogMessage(LogSeverity.Info, "Lavalink", "Failed to connect to Lavalink node. Waiting " + connectionWait / 1000 + " seconds"));
                    }

                    await Task.Delay(connectionWait);
                }
            });
        }

        /// <summary>
        /// Starts the lavalink connection
        /// </summary>
        /// <returns></returns>
        public Task StartAsync()
        {
            return Task.Run(() =>
            {
                if (discordClient.CurrentUser == null)
                    throw new InvalidOperationException("Can't connect when CurrentUser is null. Please wait until Discord connects");

                lavalinkCancellation = new CancellationTokenSource();

                // Setup the lavalink websocket connection
                webSocket = new LavalinkWebSocket(this, config);

                webSocket.OnReceive += (message) =>
                {
                    // TODO: Implement stats event
                    switch((string)message["op"])
                    {
                        case "playerUpdate":
                            {
                                Console.WriteLine(new LogMessage(LogSeverity.Debug, "Lavalink", "Received Dispatch (PLAYER_UPDATE)"));

                                ulong guildId = (ulong)message["guildId"];

                                if (players.ContainsKey(guildId))
                                {
                                    LavalinkPlayer player = players[guildId];
                                    LavalinkTrack currentTrack = player.CurrentTrack;

                                    player.FireEvent(Event.PlayerUpdate, message["state"]["position"]);
                                    PlayerUpdate?.Invoke(player, currentTrack, (long)message["state"]["position"]).GetAwaiter();
                                }

                                break;
                            }

                        case "event":
                            {
                                ulong guildId = (ulong)message["guildId"];

                                if (players.ContainsKey(guildId))
                                {
                                    LavalinkPlayer player = players[guildId];
                                    LavalinkTrack currentTrack = player.CurrentTrack;

                                    switch ((string)message["type"])
                                    {
                                        case "TrackEndEvent":
                                            {
                                                Console.WriteLine(new LogMessage(LogSeverity.Debug, "Lavalink", "Received Dispatch (TRACK_END_EVENT)"));
                                                
                                                player.FireEvent(Event.TrackEnd, message["reason"]);
                                                PlayerEnd?.Invoke(player, currentTrack, (string)message["reason"]).GetAwaiter();

                                                break;
                                            }

                                        case "TrackExceptionEvent":
                                            {
                                                Console.WriteLine(new LogMessage(LogSeverity.Debug, "Lavalink", "Received Dispatch (TRACK_EXCEPTION_EVENT)"));

                                                player.FireEvent(Event.TrackException, message["error"]);
                                                PlayerException?.Invoke(player, currentTrack, (string)message["error"]).GetAwaiter();

                                                break;
                                            }

                                        case "TrackStuckEvent":
                                            {
                                                Console.WriteLine(new LogMessage(LogSeverity.Debug, "Lavalink", "Received Dispatch (TRACK_STUCK_EVENT)"));

                                                player.FireEvent(Event.TrackStuck, message["thresholdMs"]);
                                                PlayerStuck?.Invoke(player, currentTrack, (long)message["thresholdMs"]).GetAwaiter();

                                                break;
                                            }

                                        default:
                                            {
                                                Console.WriteLine(new LogMessage(LogSeverity.Debug, "Lavalink", $"Warning: Unknown Event Type ({(string)message["type"]})"));

                                                break;
                                            }
                                    }
                                }

                                break;
                            }

                        default:
                            {
                                Console.WriteLine(new LogMessage(LogSeverity.Debug, "Lavalink", "Received Unknown Dispatch (" + ((string)message["op"]).ToUpper() + ")"));

                                break;
                            }
                    }

                    return Task.CompletedTask;
                };

                webSocket.OnClosed += (closeStatus, closeDescription) =>
                {
                    ConnectWebSocket();

                    return Task.CompletedTask;
                };

                ConnectWebSocket();
            });
        }

        /// <summary>
        /// Stops the lavalink connection
        /// </summary>
        /// <returns></returns>
        public Task StopAsync()
        {
            return Task.Run(async () =>
            {
                lavalinkCancellation.Cancel();
                await webSocket.Disconnect();
            });
        }

        /// <summary>
        /// Joins a voice channel and returns a new instance of <see cref="LavalinkPlayer"/>
        /// </summary>
        /// <param name="voiceChannel"></param>
        /// <returns></returns>
        public async Task<LavalinkPlayer> JoinAsync(IVoiceChannel voiceChannel)
        {
            if (players.ContainsKey(voiceChannel.GuildId))
                throw new InvalidOperationException("This guild is already actively connected");

            // Disconnect from the channel first for a fresh session id
            await voiceChannel.DisconnectAsync();

            LavalinkPlayer player = new LavalinkPlayer(this, voiceChannel);
            players.Add(voiceChannel.GuildId, player);

            // Initiates the voice connection
            await player.ConnectAsync();

            return player;
        }

        /// <summary>
        /// Gets a currently active <see cref="LavalinkPlayer"/> for the guild id
        /// </summary>
        /// <param name="guildId"></param>
        /// <returns></returns>
        public LavalinkPlayer GetPlayer(ulong guildId)
        {
            if (players.ContainsKey(guildId))
                return players[guildId];

            return null;
        }

        /// <summary>
        /// Leaves a voice channel
        /// </summary>
        /// <param name="guildId"></param>
        public async Task LeaveAsync(ulong guildId)
        {
            if (!players.ContainsKey(guildId))
                throw new InvalidOperationException("This guild is not actively connected");

            await players[guildId].DisconnectAsync();
        }

        /// <summary>
        /// Gets a single track from the Lavalink REST API
        /// </summary>
        /// <param name="identifier"></param>
        /// <returns></returns>
        public async Task<LavalinkTrack> GetTrack(string identifier)
        {
            // Sets up a new HttpClient and requests a track
            HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Add("Authorization", config.Authorization);

            string response = await client.GetStringAsync($"http://{config.RESTHost}:{config.RESTPort}/loadtracks?identifier={identifier}");

            return new LavalinkTrack(response);
        }
    }
}
