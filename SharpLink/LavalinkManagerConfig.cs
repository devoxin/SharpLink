using Discord;

namespace SharpLink
{
    public class LavalinkManagerConfig
    {
        // Default config follows the application.yml.example in the Lavalink GitHub located at https://github.com/Frederikam/Lavalink/blob/master/LavalinkServer/application.yml.example
        public string ServerAddress = "127.0.0.1";
        public ushort ServerPort = 2333;
        public string Authorization = "youshallnotpass";
        public int TotalShards = 1;
        public LogSeverity LogSeverity = LogSeverity.Info;
    }
}
