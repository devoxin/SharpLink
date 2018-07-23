using Newtonsoft.Json.Linq;

namespace SharpLink.Stats
{
    public class LavalinkStats
    {
        public readonly int PlayingPlayers = -1;
        public readonly MemoryStats Memory;
        public readonly int Players = -1;
        public readonly CPUStats CPU;
        public readonly long Uptime = -1;
        public readonly FrameStats FrameStats;

        internal LavalinkStats(JObject stats)
        {
            PlayingPlayers = (int)stats["playingPlayers"];
            Memory = new MemoryStats(stats["memory"]);
            Players = (int)stats["players"];
            CPU = new CPUStats(stats["cpu"]);
            Uptime = (long)stats["uptime"];
            FrameStats = (stats.ContainsKey("frameStats") ? new FrameStats(stats["frameStats"]) : null);
        }
    }
}
