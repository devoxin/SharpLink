using Discord;
using Discord.WebSocket;
using Newtonsoft.Json.Linq;
using SharpLink.Enums;
using System;
using System.Threading.Tasks;

namespace SharpLink
{
    public class LavalinkPlayer
    {
        private LavalinkManager manager;
        private IVoiceChannel initialVoiceChannel;
        private string sessionId = "";
        private LavalinkTrack currentTrack;

        #region PUBLIC_FIELDS
        public bool Playing { get; private set; }
        public long CurrentPosition { get; private set; }
        public LavalinkTrack CurrentTrack { get { return currentTrack; } }
        public IVoiceChannel VoiceChannel { get { return initialVoiceChannel; } }
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

            JObject data = new JObject();
            data.Add("op", "pause");
            data.Add("guildId", initialVoiceChannel.GuildId.ToString());
            data.Add("pause", true);

            await manager.GetWebSocket().SendAsync(data.ToString());

            Playing = false;
        }

        /// <summary>
        /// Resumes the player
        /// </summary>
        /// <returns></returns>
        public async Task ResumeAsync()
        {
            if (!Playing) throw new InvalidOperationException("The player is not currently paused");

            JObject data = new JObject();
            data.Add("op", "pause");
            data.Add("guildId", initialVoiceChannel.GuildId.ToString());
            data.Add("pause", false);

            await manager.GetWebSocket().SendAsync(data.ToString());

            Playing = true;
        }

        /// <summary>
        /// Stops the player but does not destroy it
        /// </summary>
        /// <returns></returns>
        public async Task StopAsync()
        {
            JObject data = new JObject();
            data.Add("op", "stop");
            data.Add("guildId", initialVoiceChannel.GuildId.ToString());

            await manager.GetWebSocket().SendAsync(data.ToString());

            Playing = false;
        }

        /// <summary>
        /// Seeks the current <see cref="LavalinkTrack"/>
        /// </summary>
        /// <param name="position"></param>
        /// <returns></returns>
        public async Task SeekAsync(int position)
        {
            JObject data = new JObject();
            data.Add("op", "seek");
            data.Add("guildId", initialVoiceChannel.GuildId.ToString());
            data.Add("position", position);

            await manager.GetWebSocket().SendAsync(data.ToString());
        }

        /// <summary>
        /// Sets the volume
        /// </summary>
        /// <param name="volume"></param>
        /// <returns></returns>
        public async Task SetVolumeAsync(uint volume)
        {
            if (volume > 150)
                throw new ArgumentOutOfRangeException("Volume cannot be more than 150");

            JObject data = new JObject();
            data.Add("op", "volume");
            data.Add("guildId", initialVoiceChannel.GuildId.ToString());
            data.Add("volume", volume);

            await manager.GetWebSocket().SendAsync(data.ToString());
        }

        internal void FireEvent(Event eventType, JToken eventData)
        {
            switch(eventType)
            {
                case Event.PlayerUpdate:
                    {
                        CurrentPosition = (long)eventData;

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
            switch(change)
            {
                case SessionChange.Connect:
                    {
                        SocketVoiceServer voiceServer = (SocketVoiceServer)changeData;
                        JObject eventData = new JObject();
                        eventData.Add("token", voiceServer.Token);
                        eventData.Add("guild_id", voiceServer.Guild.Id.ToString());
                        eventData.Add("endpoint", voiceServer.Endpoint);

                        JObject data = new JObject();
                        data.Add("op", "voiceUpdate");
                        data.Add("guildId", voiceServer.Guild.Id.ToString());
                        data.Add("sessionId", sessionId);
                        data.Add("event", eventData);

                        await manager.GetWebSocket().SendAsync(data.ToString());

                        break;
                    }

                case SessionChange.Disconnect:
                    {
                        ulong guildId = (ulong)changeData;

                        JObject data = new JObject();
                        data.Add("op", "destroy");
                        data.Add("guildId", guildId.ToString());

                        await manager.GetWebSocket().SendAsync(data.ToString());

                        break;
                    }
            }
        }
    }
}
