using System;
using Newtonsoft.Json.Linq;

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
        public readonly ulong Position;
        public readonly string Title;
        public readonly string Url;

        internal LavalinkTrack(JToken jsonTrack)
        {
            TrackId = (string) jsonTrack["track"];
            Identifier = (string) jsonTrack["info"]["identifier"];
            IsSeekable = (bool) jsonTrack["info"]["isSeekable"];
            Author = (string) jsonTrack["info"]["author"];
            IsStream = (bool) jsonTrack["info"]["isStream"];
            Length = IsStream ? TimeSpan.MaxValue : TimeSpan.FromMilliseconds((ulong) jsonTrack["info"]["length"]);
            Position = (ulong) jsonTrack["info"]["position"];
            Title = (string) jsonTrack["info"]["title"];
            Url = (string) jsonTrack["info"]["uri"];
        }

        // This constructor is used from the TrackId parser and will contain less info
        internal LavalinkTrack(string trackId, string title, string author, ulong length, string identifer,
            bool isStream, string url, ulong position)
        {
            TrackId = trackId;
            Title = title;
            Author = author;
            Length = IsStream ? TimeSpan.MaxValue : TimeSpan.FromMilliseconds(length);
            Identifier = identifer;
            IsStream = isStream;
            Url = url;
            Position = position;
        }
    }
}