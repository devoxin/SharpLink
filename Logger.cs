using Discord;
using System;
using System.Collections.Generic;
using System.Text;

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
