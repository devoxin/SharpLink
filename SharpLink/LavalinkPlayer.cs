using Discord;
using Discord.Audio;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpLink
{
    public class LavalinkPlayer
    {
        private LavalinkManager manager;
        private IVoiceChannel initialVoiceChannel;
        private IAudioClient audioClient;
        private string sessionId = "";

        // TODO: Implement events

        internal LavalinkPlayer(LavalinkManager manager, IVoiceChannel voiceChannel)
        {
            this.manager = manager;
            initialVoiceChannel = voiceChannel;
            sessionId = voiceChannel.Guild.GetUserAsync(manager.GetDiscordClient().CurrentUser.Id).GetAwaiter().GetResult().VoiceSessionId;
        }

        internal async Task ConnectAsync()
        {
            // NOTE: This is currently using a custom library where this method is valid.
            // The custom library implements an AudioClient creation bypass
            audioClient = await initialVoiceChannel.ConnectAsync(true);
        }

        public async Task PlayAsync(LavalinkTrack track)
        {
            await manager.PlayTrackAsync(track.TrackId, initialVoiceChannel.GuildId);
        }

        internal void SetSessionId(string voiceSessionId)
        {
            sessionId = voiceSessionId;
        }

        internal string GetSessionId()
        {
            return sessionId;
        }
    }
}
