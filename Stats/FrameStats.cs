using Newtonsoft.Json.Linq;

namespace SharpLink.Stats
{
    public class FrameStats
    {
        public readonly int Sent = -1;
        public readonly int Deficit = -1;
        public readonly int Nulled = -1;

        internal FrameStats(JToken frameStats)
        {
            Sent = (int)frameStats["sent"];
            Deficit = (int)frameStats["deficit"];
            Nulled = (int)frameStats["nulled"];
        }
    }
}
