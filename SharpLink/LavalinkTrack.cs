using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpLink
{
    public class LavalinkTrack
    {
        public string TrackId;
        public string Identifier;
        public bool IsSeekable;
        public string Author;
        public TimeSpan Length;
        public bool IsStream;
        public int Position;
        public string Title;
        public string Url;

        internal LavalinkTrack(string trackJson)
        {
            JArray json = JArray.Parse(trackJson);
            JToken jsonTrack = json.First;

            TrackId = (string)jsonTrack["track"];
            Identifier = (string)jsonTrack["info"]["identifier"];
            IsSeekable = (bool)jsonTrack["info"]["isSeekable"];
            Author = (string)jsonTrack["info"]["author"];
            Length = TimeSpan.FromMilliseconds((int)jsonTrack["info"]["length"]);
            IsStream = (bool)jsonTrack["info"]["isStream"];
            Position = (int)jsonTrack["info"]["position"];
            Title = (string)jsonTrack["info"]["title"];
            Url = (string)jsonTrack["info"]["uri"];
        }
    }
}
