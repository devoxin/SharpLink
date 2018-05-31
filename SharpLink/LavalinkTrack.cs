using Newtonsoft.Json.Linq;
using System;

namespace SharpLink
{
    public class LavalinkTrack
    {
        public readonly string TrackId;
        public readonly string Identifier;
        public readonly bool IsSeekable;
        public readonly string Author;
        public readonly TimeSpan Length;
        public readonly bool IsStream;
        public readonly int Position;
        public readonly string Title;
        public readonly string Url;

        internal LavalinkTrack(string trackJson)
        {
            JArray json = JArray.Parse(trackJson);
            JToken jsonTrack = json.First;

            TrackId = (string)jsonTrack["track"];
            Identifier = (string)jsonTrack["info"]["identifier"];
            IsSeekable = (bool)jsonTrack["info"]["isSeekable"];
            Author = (string)jsonTrack["info"]["author"];
            IsStream = (bool)jsonTrack["info"]["isStream"];
            Length = (IsStream ? TimeSpan.MaxValue : TimeSpan.FromMilliseconds((int)jsonTrack["info"]["length"]));
            Position = (int)jsonTrack["info"]["position"];
            Title = (string)jsonTrack["info"]["title"];
            Url = (string)jsonTrack["info"]["uri"];
        }
    }
}
