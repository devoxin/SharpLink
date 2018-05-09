using Discord;
using Discord.WebSocket;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace SharpLink
{
    public class LavalinkManager
    {
        private LavalinkWebSocket webSocket;
        private LavalinkManagerConfig config;
        private DiscordSocketClient discordClient;
        private Dictionary<ulong, LavalinkPlayer> players = new Dictionary<ulong, LavalinkPlayer>();

        /// <summary>
        /// Initiates a new Lavalink node connection
        /// </summary>
        /// <param name="discordClient"></param>
        /// <param name="config"></param>
        public LavalinkManager(DiscordSocketClient discordClient, LavalinkManagerConfig config)
        {
            this.config = config;
            this.discordClient = discordClient;

            // Setup the socket client events
            discordClient.VoiceServerUpdated += (voiceServer) =>
            {
                Console.WriteLine(new LogMessage(LogSeverity.Debug, "Lavalink", "VOICE_SERVER_UPDATE(" + voiceServer.Guild.Id + ")"));

                JObject eventData = new JObject();
                eventData.Add("token", voiceServer.Token);
                eventData.Add("guild_id", voiceServer.Guild.Id.ToString());
                eventData.Add("endpoint", voiceServer.Endpoint);

                JObject data = new JObject();
                data.Add("op", "voiceUpdate");
                data.Add("guildId", voiceServer.Guild.Id.ToString());
                data.Add("sessionId", players[voiceServer.Guild.Id]?.GetSessionId());
                data.Add("event", eventData);

                Task.Run(async () =>
                {
                    await webSocket.SendAsync(data.ToString());
                });

                return Task.CompletedTask;
            };

            discordClient.UserVoiceStateUpdated += (user, oldVoiceState, newVoiceState) =>
            {
                // We only need voice state updates for the current user
                if (user.Id == discordClient.CurrentUser.Id)
                {
                    if (oldVoiceState.VoiceChannel == null && newVoiceState.VoiceChannel != null)
                    {
                        Console.WriteLine(new LogMessage(LogSeverity.Debug, "Lavalink", "VOICE_STATE_UPDATE(" + newVoiceState.VoiceChannel.Guild.Id + ", Connected)"));

                        // Connected
                        players[newVoiceState.VoiceChannel.Guild.Id]?.SetSessionId(newVoiceState.VoiceSessionId);
                    }
                    else if (oldVoiceState.VoiceChannel != null && newVoiceState.VoiceChannel == null)
                    {
                        Console.WriteLine(new LogMessage(LogSeverity.Debug, "Lavalink", "VOICE_STATE_UPDATE(" + oldVoiceState.VoiceChannel.Guild.Id + ", Disconnected)"));

                        // Disconnected
                        players[oldVoiceState.VoiceChannel.Guild.Id]?.SetSessionId("");
                    }
                }

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

        public Task StartAsync()
        {
            return Task.Run(() =>
            {
                if (discordClient.CurrentUser == null)
                    throw new InvalidOperationException("Can't connect when CurrentUser is null. Please wait until Discord connects");

                // Setup the lavalink websocket connection
                webSocket = new LavalinkWebSocket(this, config);
                webSocket.Connect();
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
        public void Leave(ulong guildId)
        {
            if (!players.ContainsKey(guildId))
                throw new InvalidOperationException("This guild is not actively connected");

            // TODO: Disconnect the guild
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
