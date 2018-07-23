using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Newtonsoft.Json.Linq;
using SharpLink.Events;

namespace SharpLink
{
    internal class LavalinkWebSocket
    {
        private ClientWebSocket webSocket;
        private Uri hostUri;
        private Boolean Connected;
        private LavalinkManager manager;
        private LavalinkManagerConfig config;

        #region EVENTS
        public event AsyncEvent<JObject> OnReceive;
        public event AsyncEvent<WebSocketCloseStatus?, string> OnClosed;
        #endregion

        internal LavalinkWebSocket(LavalinkManager manager, LavalinkManagerConfig config)
        {
            this.manager = manager;
            this.config = config;
            hostUri = new Uri($"ws://{config.WebSocketHost}:{config.WebSocketPort}");
        }

        private async Task ConnectWebSocketAsync()
        {
            webSocket = new ClientWebSocket();
            webSocket.Options.SetRequestHeader("Authorization", config.Authorization);
            webSocket.Options.SetRequestHeader("Num-Shards", config.TotalShards.ToString());
            webSocket.Options.SetRequestHeader("User-Id", manager.GetDiscordClient().CurrentUser.Id.ToString());

            manager.logger.Log($"Connecting to Lavalink node at {GetHostUri()}", LogSeverity.Info);
            await webSocket.ConnectAsync(hostUri, CancellationToken.None);
            Connected = true;
            manager.logger.Log($"Connected to Lavalink node", LogSeverity.Info);

            while (webSocket.State == WebSocketState.Open)
            {
                var jsonString = await ReceiveAsync(webSocket);
                var json = JObject.Parse(jsonString);

                OnReceive?.InvokeAsync(json);
            }
        }

        private async Task DisconnectWebSocketAsync()
        {
            if (webSocket != null)
                await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
        }

        private async Task<string> ReceiveAsync(ClientWebSocket webSocket)
        {
            // TODO: Probably should optimize this (better resource allocation/management)

            var temporaryBuffer = new byte[8192];
            var buffer = new byte[8192 * 16];
            var offset = 0;
            var end = false;

            while (!end)
            {
                var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(temporaryBuffer), CancellationToken.None);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    OnClosed?.InvokeAsync(result.CloseStatus, result.CloseStatusDescription);
                    Connected = false;
                    manager.logger.Log($"Disconnected from Lavalink node ({(int)result.CloseStatus}, {result.CloseStatusDescription})", LogSeverity.Info);
                }
                else
                {
                    temporaryBuffer.CopyTo(buffer, offset);
                    offset += result.Count;
                    temporaryBuffer = new byte[8192];

                    if (result.EndOfMessage)
                    {
                        end = true;
                    }
                }
            }

            return Encoding.UTF8.GetString(buffer);
        }

        internal Boolean IsConnected()
        {
            return webSocket != null && Connected;
        }

        internal async Task SendAsync(string message)
        {
            var messageBytes = Encoding.UTF8.GetBytes(message);
            await webSocket.SendAsync(new ArraySegment<byte>(messageBytes), WebSocketMessageType.Text, true, CancellationToken.None);
        }

        internal async Task Connect()
        {
            await ConnectWebSocketAsync();
        }

        internal async Task Disconnect()
        {
            await DisconnectWebSocketAsync();
        }

        internal string GetHostUri()
        {
            return hostUri.AbsoluteUri;
        }
    }
}
