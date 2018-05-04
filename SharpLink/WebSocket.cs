using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WebSocketSharp;

namespace SharpLink
{
    class WebSocket
    {
        private WebSocketSharp.WebSocket webSocket;
        private Uri hostUri;
        private Boolean Connected = false;

        public WebSocket(String host, int port)
        {
            hostUri = new Uri($"{hostUri}:{port}");
            webSocket = new WebSocketSharp.WebSocket(hostUri.ToString());
            webSocket.OnOpen += OnConnect;
            webSocket.OnClose += OnDisconnect;
            // Initiate websocket connection and stuff here
        }

        public WebSocketSharp.WebSocket GetWebSocket()
        {
            return webSocket;
        }

        public Boolean IsConnected()
        {
            return webSocket != null && Connected;
        }

        #region WS_EVENTS
        public void OnConnect(Object sender, EventArgs e)
        {
            Connected = true;
        }

        public void OnDisconnect(Object sender, CloseEventArgs e)
        {
            Connected = false;
            // TODO: Logging
        }
        #endregion

    }
}
