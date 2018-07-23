using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace SharpLink.Stats
{
    public class LavalinkStats
    {
        public readonly int PlayingPlayers = -1;
        public readonly MemoryStats Memory = null;
        public readonly int Players = -1;
        public readonly CPUStats CPU = null;
        public readonly long Uptime = -1;
        public readonly FrameStats FrameStats = null;

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
