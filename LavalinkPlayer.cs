using System;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Newtonsoft.Json.Linq;
using SharpLink.Enums;

namespace SharpLink
{
    // TODO: Add IDisposable
    public class LavalinkPlayer
    {
        private LavalinkManager manager;
        private LavalinkTrack currentTrack;
        private string sessionId = string.Empty;
        private IVoiceChannel initialVoiceChannel;

        #region PUBLIC_FIELDS

        public bool Playing { get; private set; }
        public long CurrentPosition { get; private set; }
        public LavalinkTrack CurrentTrack => currentTrack;
        public IVoiceChannel VoiceChannel => initialVoiceChannel;

        #endregion

        internal LavalinkPlayer(LavalinkManager manager, IVoiceChannel voiceChannel)
        {
            this.manager = manager;
            initialVoiceChannel = voiceChannel;
        }

        internal async Task ConnectAsync()
        {
            await initialVoiceChannel.ConnectAsync(true, false, true);
        }

        // TODO: Implement MoveAsync()/MoveNodeAsync() where we explicitly define that we're moving Lavalink servers

        /// <summary>
        /// Tells lavalink to destroy the voice connection and disconnects from the channel
        /// </summary>
        /// <returns></returns>
        public async Task DisconnectAsync()
        {
            await initialVoiceChannel.DisconnectAsync();
            await UpdateSessionAsync(SessionChange.Disconnect, initialVoiceChannel.GuildId);
            await manager.RemovePlayerAsync(initialVoiceChannel.GuildId);

            Playing = false;
        }

        /// <summary>
        /// Plays the designated <see cref="LavalinkTrack"/>
        /// </summary>
        /// <param name="track"></param>
        /// <returns></returns>
        public async Task PlayAsync(LavalinkTrack track)
        {
            currentTrack = track;
            await manager.PlayTrackAsync(track.TrackId, initialVoiceChannel.GuildId);
            Playing = true;
        }

        /// <summary>
        /// Pauses the player
        /// </summary>
        /// <returns></returns>
        public async Task PauseAsync()
        {
            if (!Playing) throw new InvalidOperationException("The player is currently paused");

            var data = new JObject
            {
                {"op", "pause"},
                {"guildId", $"{initialVoiceChannel.GuildId}"},
                {"pause", true}
            };
            await manager.GetWebSocket().SendAsync($"{data}");
            Playing = false;
        }

        /// <summary>
        /// Resumes the player
        /// </summary>
        /// <returns></returns>
        public async Task ResumeAsync()
        {
            if (Playing) throw new InvalidOperationException("The player is not currently paused");

            var data = new JObject
            {
                {"op", "pause"},
                {"guildId", $"{initialVoiceChannel.GuildId}"},
                {"pause", false}
            };
            await manager.GetWebSocket().SendAsync($"{data}");
            Playing = true;
        }

        /// <summary>
        /// Stops the player but does not destroy it
        /// </summary>
        /// <returns></returns>
        public async Task StopAsync()
        {
            var data = new JObject {{"op", "stop"}, {"guildId", $"{initialVoiceChannel.GuildId}"}};
            await manager.GetWebSocket().SendAsync($"{data}");
            Playing = false;
        }

        /// <summary>
        /// Seeks the current <see cref="LavalinkTrack"/>
        /// </summary>
        /// <param name="position"></param>
        /// <returns></returns>
        public async Task SeekAsync(int position)
        {
            var data = new JObject
            {
                {"op", "seek"},
                {"guildId", $"{initialVoiceChannel.GuildId}"},
                {"position", position}
            };

            await manager.GetWebSocket().SendAsync($"{data}");
        }

        /// <summary>
        /// Sets the volume
        /// </summary>
        /// <param name="volume"></param>
        /// <returns></returns>
        public async Task SetVolumeAsync(uint volume)
        {
            if (volume > 150)
                throw new ArgumentOutOfRangeException(nameof(volume), "Volume cannot be more than 150");

            var data = new JObject
            {
                {"op", "volume"},
                {"guildId", $"{initialVoiceChannel.GuildId}"},
                {"volume", volume}
            };

            await manager.GetWebSocket().SendAsync($"{data}");
        }

        internal void FireEvent(Event eventType, JToken eventData)
        {
            switch (eventType)
            {
                case Event.PlayerUpdate:
                {
                    CurrentPosition = (long) eventData;
                    break;
                }

                case Event.TrackEnd:
                {
                    currentTrack = null;
                    Playing = true;
                    break;
                }

                case Event.TrackException:
                {
                    currentTrack = null;
                    Playing = false;
                    break;
                }

                case Event.TrackStuck:
                {
                    currentTrack = null;
                    Playing = false;
                    break;
                }
            }
        }

        internal void SetSessionId(string voiceSessionId)
        {
            sessionId = voiceSessionId;
        }

        internal string GetSessionId()
        {
            return sessionId;
        }

        internal async Task UpdateSessionAsync(SessionChange change, object changeData = null)
        {
            switch (change)
            {
                case SessionChange.Connect:
                {
                    var voiceServer = (SocketVoiceServer) changeData;
                    var eventData = new JObject
                    {
                        {"token", voiceServer.Token},
                        {"guild_id", $"{voiceServer.Guild.Id}"},
                        {"endpoint", voiceServer.Endpoint}
                    };

                    var data = new JObject
                    {
                        {"op", "voiceUpdate"},
                        {"guildId", $"{voiceServer.Guild.Id}"},
                        {"sessionId", sessionId},
                        {"event", eventData}
                    };
                    await manager.GetWebSocket().SendAsync($"{data}");
                    break;
                }

                case SessionChange.Disconnect:
                {
                    var guildId = (ulong) changeData;
                    var data = new JObject {{"op", "destroy"}, {"guildId", $"{guildId}"}};
                    await manager.GetWebSocket().SendAsync($"{data}");
                    break;
                }
            }
        }
    }
}