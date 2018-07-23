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
        private int Tries;
        private bool Connected;
        private readonly Uri HostUri;
        private ClientWebSocket webSocket;
        private readonly LavalinkManager Manager;
        private readonly LavalinkManagerConfig Config;

        #region EVENTS

        public event AsyncEvent<JObject> OnReceive;
        public event AsyncEvent<WebSocketCloseStatus?, string> OnClosed;

        #endregion

        internal LavalinkWebSocket(LavalinkManager manager, LavalinkManagerConfig config)
        {
            Config = config;
            Manager = manager;
            HostUri = new Uri($"ws://{Config.WebSocketHost}:{Config.WebSocketPort}");
        }

        private async Task ConnectWebSocketAsync()
        {
            webSocket = new ClientWebSocket();
            webSocket.Options.SetRequestHeader("Authorization", Config.Authorization);
            webSocket.Options.SetRequestHeader("Num-Shards", Config.TotalShards.ToString());
            webSocket.Options.SetRequestHeader("User-Id", Manager.GetDiscordClient().CurrentUser.Id.ToString());

            Manager.logger.Log($"Connecting to Lavalink node at {GetHostUri()}");
            await webSocket.ConnectAsync(HostUri, CancellationToken.None);
            Connected = true;
            Manager.logger.Log("Connected to Lavalink node");

            while (webSocket.State == WebSocketState.Open && !(Tries > Config.MaxNumberOfTries))
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

        private async Task<string> ReceiveAsync(WebSocket socket)
        {
            // TODO: Probably should optimize this (better resource allocation/management)

            var temporaryBuffer = new byte[8192];
            var buffer = new byte[8192 * 16];
            var offset = 0;
            var end = false;

            while (!end)
            {
                var result =
                    await socket.ReceiveAsync(new ArraySegment<byte>(temporaryBuffer), CancellationToken.None);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    OnClosed?.InvokeAsync(result.CloseStatus, result.CloseStatusDescription);
                    Connected = false;
                    Manager.logger.Log(
                        $"Disconnected from Lavalink node ({(int) result.CloseStatus}, {result.CloseStatusDescription})");
                    Tries++;
                    if (Tries >= Config.MaxNumberOfTries)
                        Manager.logger.Log("Maximum number of tries reached.", LogSeverity.Critical);
                }
                else
                {
                    temporaryBuffer.CopyTo(buffer, offset);
                    offset += result.Count;
                    temporaryBuffer = new byte[8192];

                    if (result.EndOfMessage)
                        end = true;

                    if (Tries > 0)
                        Tries = 0;
                }
            }

            return Encoding.UTF8.GetString(buffer);
        }

        internal bool IsConnected()
        {
            return webSocket != null && Connected;
        }

        internal async Task SendAsync(string message)
        {
            var messageBytes = Encoding.UTF8.GetBytes(message);
            await webSocket.SendAsync(new ArraySegment<byte>(messageBytes), WebSocketMessageType.Text, true,
                CancellationToken.None);
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
            return HostUri.AbsoluteUri;
        }
    }
}