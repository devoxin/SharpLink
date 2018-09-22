namespace SharpLink.Enums
{
    internal enum SessionChange
    {
        Connect,
        Disconnect,
        // This will allow the session to resume if discord reconnects
        ConnectionLost,
        MoveNode,
    }
}
