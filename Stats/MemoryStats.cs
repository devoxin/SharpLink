using Newtonsoft.Json.Linq;

namespace SharpLink.Stats
{
    public class MemoryStats
    {
        public readonly long Reservable = -1;
        public readonly long Used = -1;
        public readonly long Free = -1;
        public readonly long Allocated = -1;

        internal MemoryStats(JToken memoryStats)
        {
            Reservable = (long)memoryStats["reservable"];
            Used = (long)memoryStats["used"];
            Free = (long)memoryStats["free"];
            Allocated = (long)memoryStats["allocated"];
        }
    }
}
