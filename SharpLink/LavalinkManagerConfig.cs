namespace SharpLink
{
    public class LavalinkManagerConfig
    {
        // Default config follows the application.yml.example in the Lavalink GitHub located at https://github.com/Frederikam/Lavalink/blob/master/LavalinkServer/application.yml.example
        public string WebSocketHost = "0.0.0.0";
        public ushort WebSocketPort = 80;
        public string RESTHost = "0.0.0.0";
        public ushort RESTPort = 2333;
        public string Authorization = "youshallnotpass";
        public int TotalShards = 1;
    }
}
