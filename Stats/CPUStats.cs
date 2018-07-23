using Newtonsoft.Json.Linq;

namespace SharpLink.Stats
{
    public class CPUStats
    {
        public readonly int Cores = -1;
        public readonly float SystemLoad = -1;
        public readonly float LavalinkLoad = -1;

        internal CPUStats(JToken cpuStats)
        {
            Cores = (int)cpuStats["cores"];
            SystemLoad = (float)cpuStats["systemLoad"];
            LavalinkLoad = (float)cpuStats["lavalinkLoad"];
        }
    }
}
