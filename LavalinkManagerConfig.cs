using System;
using Discord;

namespace SharpLink
{
    public class LavalinkManagerConfig
    {
        public LavalinkManagerConfig()
        {
            if (string.IsNullOrWhiteSpace(WebSocketHost))
                throw new ArgumentNullException(nameof(WebSocketHost));
            if (string.IsNullOrWhiteSpace(RESTHost))
                throw new ArgumentNullException(nameof(RESTHost));
            if (MaxNumberOfTries < 0)
                throw new InvalidOperationException($"{nameof(MaxNumberOfTries)} cannot be lower than 0.");
        }

        public string WebSocketHost = "0.0.0.0";
        public ushort WebSocketPort = 80;
        public string RESTHost = "0.0.0.0";
        public ushort RESTPort = 2333;
        public string Authorization = "youshallnotpass";
        public int TotalShards = 1;
        public LogSeverity LogSeverity = LogSeverity.Info;

        /// <summary>
        /// Tries when trying to connect to Lavalink.
        /// </summary>
        public int MaxNumberOfTries = 0;
    }
}