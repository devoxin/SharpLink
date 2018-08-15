using Newtonsoft.Json.Linq;
using SharpLink.Enums;
using SharpLink.Rest;
using System;
using System.Collections.Generic;
using System.Text;

namespace SharpLink
{
    public class LoadTracksResponse
    {
        /// <summary>
        /// The type of response this is.
        /// </summary>
        /// <remarks>Returns null for Lavalink version 2 and under</remarks>
        public LoadType? LoadType = null;
        /// <summary>
        /// The tracks in the response
        /// </summary>
        public IReadOnlyCollection<LavalinkTrack> Tracks = null;
        /// <summary>
        /// The playlist information if the response is a playlist.
        /// </summary>
        /// <remarks>Returns null for Lavalink version 2 and under</remarks>
        public PlaylistInfo PlaylistInfo = null;

        internal LoadTracksResponse(JToken response)
        {
            // Lavalink version 2 and below versus version 3 and up
            if (response is JArray)
            {
                JArray tracksArray = response as JArray;
                List<LavalinkTrack> tracks = new List<LavalinkTrack>();
                foreach (JToken jsonTrack in tracksArray)
                {
                    tracks.Add(new LavalinkTrack(jsonTrack));
                }
            }
            else if (response is JObject && response["tracks"] != null)
            {
                JArray tracksArray = response["tracks"] as JArray;
                string loadType = (string)response["loadType"];
                List<LavalinkTrack> tracks = new List<LavalinkTrack>();
                foreach (JToken jsonTrack in tracksArray)
                {
                    tracks.Add(new LavalinkTrack(jsonTrack));
                }

                switch (loadType)
                {
                    // Track Loaded as explicitly defined by Lavalink docs means it returned a single track
                    case "TRACK_LOADED":
                        {
                            LoadType = Enums.LoadType.TrackLoaded;
                            Tracks = tracks;
                            break;
                        }

                    case "PLAYLIST_LOADED":
                        {
                            LoadType = Enums.LoadType.PlaylistLoaded;
                            PlaylistInfo = new PlaylistInfo(response["playlistInfo"]);
                            Tracks = tracks;
                            break;
                        }

                    case "SEARCH_RESULT":
                        {
                            LoadType = Enums.LoadType.SearchResult;
                            Tracks = tracks;
                            break;
                        }

                    case "NO_MATCHES":
                        {
                            LoadType = Enums.LoadType.NoMatches;
                            break;
                        }

                    case "LOAD_FAILED":
                        {
                            LoadType = Enums.LoadType.LoadFailed;
                            break;
                        }
                }
            }
        }
    }
}
