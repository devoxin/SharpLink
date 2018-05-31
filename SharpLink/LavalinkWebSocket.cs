using Discord;
using Newtonsoft.Json.Linq;
using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SharpLink
{
    internal class LavalinkWebSocket
    {
        private ClientWebSocket webSocket;
        private Uri hostUri;
        private Boolean Connected = false;
        private LavalinkManager manager;
        private LavalinkManagerConfig config;

        #region EVENTS
        public event Func<JObject, Task> OnReceive;
        public event Func<WebSocketCloseStatus?, string, Task> OnClosed;
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

            Console.WriteLine(new LogMessage(LogSeverity.Info, "Lavalink", "Connecting to Lavalink node"));
            await webSocket.ConnectAsync(hostUri, CancellationToken.None);
            Connected = true;
            Console.WriteLine(new LogMessage(LogSeverity.Info, "Lavalink", "Connected to Lavalink node"));

            while (webSocket.State == WebSocketState.Open)
            {
                string jsonString = await ReceiveAsync(webSocket);
                JObject json = JObject.Parse(jsonString);

                OnReceive?.Invoke(json).ConfigureAwait(false);
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

            byte[] temporaryBuffer = new byte[8192];
            byte[] buffer = new byte[8192 * 16];
            int offset = 0;
            bool end = false;

            while (!end)
            {
                WebSocketReceiveResult result = await webSocket.ReceiveAsync(new ArraySegment<byte>(temporaryBuffer), CancellationToken.None);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    OnClosed?.Invoke(result.CloseStatus, result.CloseStatusDescription).GetAwaiter();
                    Connected = false;
                    Console.WriteLine(new LogMessage(LogSeverity.Info, "Lavalink", "Disconnected from Lavalink node"));
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
            byte[] messageBytes = Encoding.UTF8.GetBytes(message);
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
    }
}
