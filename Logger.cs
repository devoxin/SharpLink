using System;
using Discord;

namespace SharpLink
{
    internal class Logger
    {
        private LavalinkManager lavalinkManager;
        private string prefix;

        internal Logger(LavalinkManager lavalinkManager, string prefix)
        {
            this.lavalinkManager = lavalinkManager;
            this.prefix = prefix;
        }

        internal void Log(string message, LogSeverity severity = LogSeverity.Info, Exception ex = null)
        {
            if (severity <= lavalinkManager.GetConfig().LogSeverity)
                lavalinkManager.InvokeLog(new LogMessage(severity, prefix, message, ex));
        }
    }
}
