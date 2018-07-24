using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Newtonsoft.Json.Linq;
using SharpLink.Enums;
using SharpLink.Events;
using SharpLink.Stats;

namespace SharpLink
{
    public class LavalinkManager
    {
        private int Tries;
        private LavalinkWebSocket webSocket;
        private readonly LavalinkManagerConfig config;
        private readonly BaseSocketClient baseDiscordClient;
        private readonly SemaphoreSlim playerLock = new SemaphoreSlim(1, 1);
        private readonly Dictionary<ulong, LavalinkPlayer> players = new Dictionary<ulong, LavalinkPlayer>();
        private int connectionWait = 3000;
        private CancellationTokenSource lavalinkCancellation;
        private readonly HttpClient client = new HttpClient();
        internal Logger logger;

        #region PUBLIC_EVENTS 

        public event AsyncEvent<LavalinkPlayer, LavalinkTrack, long> PlayerUpdate;
        public event AsyncEvent<LavalinkPlayer, LavalinkTrack, string> TrackEnd;
        public event AsyncEvent<LavalinkPlayer, LavalinkTrack, long> TrackStuck;
        public event AsyncEvent<LavalinkPlayer, LavalinkTrack, string> TrackException;
        public event AsyncEvent<LogMessage> Log;
        public event AsyncEvent<LavalinkStats> Stats;

        #endregion

        /// <summary>
        /// Initiates a new Lavalink node connection
        /// </summary>
        /// <param name="discordClient"></param>
        /// <param name="config"></param>
        public LavalinkManager(BaseSocketClient discordClient, LavalinkManagerConfig config = null)
        {
            this.config = config ?? new LavalinkManagerConfig();
            baseDiscordClient = discordClient;

            SetupManager();
        }

        public LavalinkManager(DiscordShardedClient discordShardedClient, LavalinkManagerConfig config = null)
        {
            this.config = config ?? new LavalinkManagerConfig();
            baseDiscordClient = discordShardedClient;

            SetupManager();
        }

        private void SetupManager()
        {
            logger = new Logger(this, "Lavalink");
            client.DefaultRequestHeaders.Add("Authorization", config.Authorization);

            baseDiscordClient.VoiceServerUpdated += async voiceServer =>
            {
                logger.Log($"VOICE_SERVER_UPDATE({voiceServer.Guild.Id}, Updating Session)", LogSeverity.Debug);
                await players[voiceServer.Guild.Id]?.UpdateSessionAsync(SessionChange.Connect, voiceServer);
            };

            baseDiscordClient.UserVoiceStateUpdated += async (user, oldVoiceState, newVoiceState) =>
            {
                if (user.Id == baseDiscordClient.CurrentUser.Id)
                {
                    if (oldVoiceState.VoiceChannel == null && newVoiceState.VoiceChannel != null)
                    {
                        logger.Log($"VOICE_STATE_UPDATE({newVoiceState.VoiceChannel.Guild.Id}, Connected)",
                            LogSeverity.Debug);

                        // Connected
                        var player = players[newVoiceState.VoiceChannel.Guild.Id];
                        player?.SetSessionId(newVoiceState.VoiceSessionId);
                    }
                    else if (oldVoiceState.VoiceChannel != null && newVoiceState.VoiceChannel == null)
                    {
                        logger.Log($"VOICE_STATE_UPDATE({newVoiceState.VoiceChannel.Guild.Id}, Disconnected)",
                            LogSeverity.Debug);

                        // Disconnected
                        var player = players[oldVoiceState.VoiceChannel.Guild.Id];
                        if (player != null)
                        {
                            player.SetSessionId(string.Empty);
                            await player.UpdateSessionAsync(SessionChange.Disconnect,
                                oldVoiceState.VoiceChannel.Guild.Id);
                            players.Remove(oldVoiceState.VoiceChannel.Guild.Id);
                        }
                    }
                }
            };

            switch (baseDiscordClient)
            {
                case DiscordShardedClient shardedClient:
                    shardedClient.ShardDisconnected += async (exception, socketClient) =>
                    {
                        await playerLock.WaitAsync();
                        foreach (var guild in socketClient.Guilds)
                        {
                            if (!players.ContainsKey(guild.Id)) continue;
                            await players[guild.Id].DisconnectAsync();
                            players.Remove(guild.Id);
                        }

                        playerLock.Release();
                    };
                    break;
                case DiscordSocketClient socketClient:
                    socketClient.Disconnected += async exception =>
                    {
                        await playerLock.WaitAsync();
                        foreach (var player in players)
                        {
                            await player.Value.DisconnectAsync();
                        }

                        players.Clear();
                        playerLock.Release();
                    };
                    break;
            }
        }

        internal async Task PlayTrackAsync(string trackId, ulong guildId)
        {
            var data = new JObject {{"op", "play"}, {"guildId", $"{guildId}"}, {"track", trackId}};
            await webSocket.SendAsync($"{data}");
        }

        internal BaseSocketClient GetDiscordClient()
        {
            return baseDiscordClient;
        }

        internal LavalinkWebSocket GetWebSocket()
        {
            return webSocket;
        }

        internal void InvokeLog(LogMessage message)
        {
            Log?.InvokeAsync(message);
        }

        internal LavalinkManagerConfig GetConfig()
        {
            return config;
        }

        internal async Task RemovePlayerAsync(ulong guildId)
        {
            await playerLock.WaitAsync();

            if (players.ContainsKey(guildId))
                players.Remove(guildId);

            playerLock.Release();
        }

        private void ConnectWebSocket()
        {
            // Continuously attempt connections to Lavalink
            Task.Run(async () =>
            {
                while (!webSocket.IsConnected)
                {
                    if (lavalinkCancellation.IsCancellationRequested)
                        break;
                    if (Tries >= config.MaxNumberOfTries && config.MaxNumberOfTries != 0)
                    {
                        logger.Log("Maximum number of tries reached.", LogSeverity.Warning);
                        break;
                    }

                    try
                    {
                        await webSocket.Connect();
                    }
                    catch (Exception ex)
                    {
                        logger.Log("Exception", LogSeverity.Debug, ex);
                    }
                    finally
                    {
                        if (!webSocket.IsConnected)
                        {
                            connectionWait += 1000;
                            logger.Log(
                                $"Waiting {connectionWait / 1000}s before re-establishing connection at {webSocket.GetHostUri}",
                                LogSeverity.Warning);
                            Tries++;
                        }
                        else
                        {
                            Tries = 0;
                        }
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
                if (baseDiscordClient.CurrentUser == null)
                    throw new InvalidOperationException(
                        "Can't connect when CurrentUser is null. Please wait until Discord connects");

                lavalinkCancellation = new CancellationTokenSource();

                // Setup the lavalink websocket connection
                webSocket = new LavalinkWebSocket(this, config);

                webSocket.OnReceive += message =>
                {
                    // TODO: Implement stats event
                    switch ((string) message["op"])
                    {
                        case "playerUpdate":
                        {
                            logger.Log("Received Dispatch (PLAYER_UPDATE)", LogSeverity.Debug);

                            var guildId = (ulong) message["guildId"];

                            if (players.ContainsKey(guildId))
                            {
                                var player = players[guildId];
                                var currentTrack = player.CurrentTrack;

                                player.FireEvent(Event.PlayerUpdate, message["state"]["position"]);
                                PlayerUpdate?.InvokeAsync(player, currentTrack, (long) message["state"]["position"]);
                            }

                            break;
                        }

                        case "event":
                        {
                            var guildId = (ulong) message["guildId"];

                            if (players.ContainsKey(guildId))
                            {
                                var player = players[guildId];
                                var currentTrack = player.CurrentTrack;

                                switch ((string) message["type"])
                                {
                                    case "TrackEndEvent":
                                    {
                                        logger.Log("Received Dispatch (TRACK_END_EVENT)", LogSeverity.Debug);

                                        player.FireEvent(Event.TrackEnd, message["reason"]);
                                        TrackEnd?.InvokeAsync(player, currentTrack, (string) message["reason"]);

                                        break;
                                    }

                                    case "TrackExceptionEvent":
                                    {
                                        logger.Log("Received Dispatch (TRACK_EXCEPTION_EVENT)", LogSeverity.Debug);

                                        player.FireEvent(Event.TrackException, message["error"]);
                                        TrackException?.InvokeAsync(player, currentTrack, (string) message["error"]);

                                        break;
                                    }

                                    case "TrackStuckEvent":
                                    {
                                        logger.Log("Received Dispatch (TRACK_STUCK_EVENT)", LogSeverity.Debug);

                                        player.FireEvent(Event.TrackStuck, message["thresholdMs"]);
                                        TrackStuck?.InvokeAsync(player, currentTrack, (long) message["thresholdMs"]);

                                        break;
                                    }

                                    default:
                                    {
                                        logger.Log($"Received Unknown Event Type {(string) message["type"]}",
                                            LogSeverity.Debug);

                                        break;
                                    }
                                }
                            }

                            break;
                        }

                        case "stats":
                        {
                            Stats?.InvokeAsync(new LavalinkStats(message));

                            break;
                        }

                        default:
                        {
                            logger.Log($"Received Uknown Dispatch ({(string) message["op"]})", LogSeverity.Debug);

                            break;
                        }
                    }

                    return Task.CompletedTask;
                };

                webSocket.OnClosed += async (closeStatus, closeDescription) =>
                {
                    await playerLock.WaitAsync();

                    players.Clear();
                    ConnectWebSocket();

                    playerLock.Release();
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

            var player = new LavalinkPlayer(this, voiceChannel);
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
            return players.ContainsKey(guildId) ? players[guildId] : null;
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

        private async Task<JArray> RequestLoadTracksAsync(string identifier)
        {
            var requestTime = DateTime.UtcNow;
            var response =
                await client.GetStringAsync(
                    $"http://{config.RESTHost}:{config.RESTPort}/loadtracks?identifier={identifier}");
            logger.Log($"GET loadtracks: {(DateTime.UtcNow - requestTime).TotalMilliseconds} ms", LogSeverity.Verbose);

            // Lavalink version 2 and 3 support
            var json = JToken.Parse(response);
            switch (json)
            {
                case JArray array:
                    return array;
                case JObject _ when json["tracks"] != null:
                    return json["tracks"] as JArray;
                default:
                    return null;
            }
        }

        /// <summary>
        /// Gets a single track from the Lavalink REST API
        /// </summary>
        /// <param name="identifier"></param>
        /// <returns></returns>
        public async Task<LavalinkTrack> GetTrackAsync(string identifier)
        {
            var json = await RequestLoadTracksAsync(identifier);
            if (json == null)
                return null;

            if (json.Count == 0)
                return null;

            var jsonTrack = json.First;
            return new LavalinkTrack(jsonTrack);
        }

        /// <summary>
        /// Gets a track from a track id
        /// </summary>
        /// <param name="trackId"></param>
        /// <remarks>Some properties may be missing from the track</remarks>
        /// <returns></returns>
        public LavalinkTrack GetTrackFromId(string trackId)
        {
            #region BYTE_REF

            /*
                Thank you sedmelluq for this info

                all integers are big-endian.
                text is 2-byte integer for length of bytes + utf8 bytes

                4 bytes size + flags (2 highest order bits are flags, the rest is size)
                1 byte of message version
                title (text)
                author (text)
                length (8-byte integer)
                identifier (text)
                1 byte boolean of whether it is a stream
                1 byte boolean of whether url field is present
                url (text)
                source name (text)
                if source is http or local, then: container type (text)
                position in ms (8-byte integer). always 0 for tracks provided by lavalink
             */

            #endregion

            try
            {
                var trackBytes = Convert.FromBase64String(trackId);
                var offset = 5;

                // Skipping size, flags, and message version at the moment

                var titleSize = Util.SwapEndianess(BitConverter.ToUInt16(trackBytes, offset));
                var title = Encoding.UTF8.GetString(trackBytes, offset + 2, titleSize);
                offset += 2 + titleSize;

                var authorSize = Util.SwapEndianess(BitConverter.ToUInt16(trackBytes, offset));
                var author = Encoding.UTF8.GetString(trackBytes, offset + 2, authorSize);
                offset += 2 + authorSize;

                var length = Util.SwapEndianess(BitConverter.ToUInt64(trackBytes, offset));
                offset += 8;

                var identifierSize = Util.SwapEndianess(BitConverter.ToUInt16(trackBytes, offset));
                var identifier = Encoding.UTF8.GetString(trackBytes, offset + 2, identifierSize);
                offset += 2 + identifierSize;

                var stream = BitConverter.ToBoolean(trackBytes, offset);
                offset += 1;

                var urlPresent = BitConverter.ToBoolean(trackBytes, offset);
                offset += 1;

                string url = null;
                if (urlPresent)
                {
                    var urlSize = Util.SwapEndianess(BitConverter.ToUInt16(trackBytes, offset));
                    url = Encoding.UTF8.GetString(trackBytes, offset + 2, urlSize);
                    offset += 2 + urlSize;
                }

                // Source name is not used in SharpLink outside of this method
                var sourceNameSize = Util.SwapEndianess(BitConverter.ToUInt16(trackBytes, offset));
                var sourceName = Encoding.UTF8.GetString(trackBytes, offset + 2, sourceNameSize);
                offset += 2 + sourceNameSize;

                if (sourceName == "local" || sourceName == "http")
                {
                    // This is ignored and not used but instead we skip the bytes
                    var containerTypeSize = Util.SwapEndianess(BitConverter.ToUInt16(trackBytes, offset));
                    offset += 2 + containerTypeSize;
                }

                var position = Util.SwapEndianess(BitConverter.ToUInt64(trackBytes, offset));

                return new LavalinkTrack(trackId, title, author, length, identifier, stream, url, position);
            }
            catch (ArgumentOutOfRangeException ex)
            {
                // This exception is thrown when the bytes are parsed invalidly and don't have the proper format
                throw new ArgumentException("TrackId failed to parse", ex);
            }
            catch (Exception ex)
            {
                // Any other error is likely an invalid track id
                throw new ArgumentException("TrackId is not valid", ex);
            }
        }

        /// <summary>
        /// Gets multiple track from the Lavalink REST API
        /// </summary>
        /// <param name="identifier"></param>
        /// <returns></returns>
        public async Task<IReadOnlyCollection<LavalinkTrack>> GetTracksAsync(string identifier)
        {
            var json = await RequestLoadTracksAsync(identifier);
            if (json == null)
                return null;

            var tracks = new List<LavalinkTrack>();
            foreach (var jsonTrack in json)
            {
                tracks.Add(new LavalinkTrack(jsonTrack));
            }

            return tracks;
        }
    }
}