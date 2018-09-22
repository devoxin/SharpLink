using System;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Newtonsoft.Json.Linq;
using SharpLink.Enums;

namespace SharpLink
{
    public class LavalinkPlayer
    {
        private readonly LavalinkManager manager;
        private string sessionId = "";

        internal LavalinkPlayer(LavalinkManager manager, IVoiceChannel voiceChannel)
        {
            this.manager = manager;
            VoiceChannel = voiceChannel;
        }

        internal async Task ConnectAsync()
        {
            await VoiceChannel.ConnectAsync(false, false, true);
        }

        // TODO: Implement MoveAsync()/MoveNodeAsync() where we explicitly define that we're moving Lavalink servers

        /// <summary>
        ///     Tells lavalink to destroy the voice connection and disconnects from the channel
        /// </summary>
        /// <returns></returns>
        public async Task DisconnectAsync(bool semLock = false)
        {
            await VoiceChannel.DisconnectAsync();
            await UpdateSessionAsync(SessionChange.Disconnect, VoiceChannel.GuildId);
            await manager.RemovePlayerAsync(VoiceChannel.GuildId, semLock);

            Playing = false;
        }

        /// <summary>
        ///     Plays the designated <see cref="LavalinkTrack" />
        /// </summary>
        /// <param name="track"></param>
        /// <returns></returns>
        public async Task PlayAsync(LavalinkTrack track)
        {
            CurrentTrack = track;

            await manager.PlayTrackAsync(track.TrackId, VoiceChannel.GuildId);

            Playing = true;
        }

        /// <summary>
        ///     Pauses the player
        /// </summary>
        /// <returns></returns>
        public async Task PauseAsync()
        {
            if (!Playing) throw new InvalidOperationException("The player is currently paused");

            var data = new JObject();
            data.Add("op", "pause");
            data.Add("guildId", VoiceChannel.GuildId.ToString());
            data.Add("pause", true);

            await manager.GetWebSocket().SendAsync(data.ToString());

            Playing = false;
        }

        /// <summary>
        ///     Resumes the player
        /// </summary>
        /// <returns></returns>
        public async Task ResumeAsync()
        {
            if (Playing) throw new InvalidOperationException("The player is not currently paused");

            var data = new JObject();
            data.Add("op", "pause");
            data.Add("guildId", VoiceChannel.GuildId.ToString());
            data.Add("pause", false);

            await manager.GetWebSocket().SendAsync(data.ToString());

            Playing = true;
        }

        /// <summary>
        ///     Stops the player but does not destroy it
        /// </summary>
        /// <returns></returns>
        public async Task StopAsync()
        {
            var data = new JObject();
            data.Add("op", "stop");
            data.Add("guildId", VoiceChannel.GuildId.ToString());

            await manager.GetWebSocket().SendAsync(data.ToString());

            Playing = false;
        }

        /// <summary>
        ///     Seeks the current <see cref="LavalinkTrack" />
        /// </summary>
        /// <param name="position"></param>
        /// <returns></returns>
        public async Task SeekAsync(int position)
        {
            var data = new JObject();
            data.Add("op", "seek");
            data.Add("guildId", VoiceChannel.GuildId.ToString());
            data.Add("position", position);

            await manager.GetWebSocket().SendAsync(data.ToString());
        }

        /// <summary>
        ///     Sets the volume
        /// </summary>
        /// <param name="volume"></param>
        /// <returns></returns>
        public async Task SetVolumeAsync(uint volume)
        {
            if (volume > 150)
                throw new ArgumentOutOfRangeException("Volume cannot be more than 150");

            var data = new JObject();
            data.Add("op", "volume");
            data.Add("guildId", VoiceChannel.GuildId.ToString());
            data.Add("volume", volume);

            await manager.GetWebSocket().SendAsync(data.ToString());
        }

        internal void FireEvent(Event eventType, JToken eventData)
        {
            switch (eventType)
            {
                case Event.PlayerUpdate:
                    {
                        CurrentPosition = (long)eventData;

                        break;
                    }

                case Event.TrackEnd:
                    {
                        CurrentTrack = null;
                        Playing = false;

                        break;
                    }

                case Event.TrackException:
                    {
                        CurrentTrack = null;
                        Playing = false;

                        break;
                    }

                case Event.TrackStuck:
                    {
                        CurrentTrack = null;
                        Playing = false;

                        break;
                    }

                case Event.ConnectionLost:
                    {
                        Playing = false;
                        break;
                    }
                case Event.ConnectionResumed:
                    {
                        Playing = true;
                        break;
                    }
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
                    var voiceServer = (SocketVoiceServer)changeData;
                    var eventData = new JObject();
                    eventData.Add("token", voiceServer.Token);
                    eventData.Add("guild_id", voiceServer.Guild.Id.ToString());
                    eventData.Add("endpoint", voiceServer.Endpoint);

                    var data = new JObject();
                    data.Add("op", "voiceUpdate");
                    data.Add("guildId", voiceServer.Guild.Id.ToString());
                    data.Add("sessionId", sessionId);
                    data.Add("event", eventData);

                    await manager.GetWebSocket().SendAsync(data.ToString());

                    break;
                }

            case SessionChange.Disconnect:
                {
                    var guildId = (ulong)changeData;

                    var data = new JObject();
                    data.Add("op", "destroy");
                    data.Add("guildId", guildId.ToString());

                    await manager.GetWebSocket().SendAsync(data.ToString());

                    break;
                }
        }
    }

    #region PUBLIC_FIELDS

    public bool Playing { get; private set; }
    public long CurrentPosition { get; private set; }
    public LavalinkTrack CurrentTrack { get; private set; }
    public IVoiceChannel VoiceChannel { get; }

    #endregion
}
}
