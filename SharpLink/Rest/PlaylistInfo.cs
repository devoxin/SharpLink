using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace SharpLink.Rest
{
    public class PlaylistInfo
    {
        public string Name = "";
        public int SelectedTrack = -1;

        public PlaylistInfo(JToken info)
        {
            if (info != null)
            {
                Name = (string)info["name"];
                SelectedTrack = (int)info["selectedTrack"];
            }
        }
    }
}
