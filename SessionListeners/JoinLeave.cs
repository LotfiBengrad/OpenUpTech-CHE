namespace server.SessionListeners
{
    using OpenUp.Networking;
    using System;
    using System.Net.Sockets;
    using OpenUp.Networking.LogMessages;
    using TCPTools;
    using Utils;

    public static class JoinLeave
    {
        public static Connection.OnSessionJoinHandler OnJoin(Connection client) => session =>
        {
            SessionManager.Instance.connections[session.id].Add(client);
            
            Console.WriteLine($"This is the session joined {session.name}");
            Console.WriteLine($"It has {session.uiHistory.Count} actions");
            
            ErrorLogger.LogMessage($"Client {client.id} joined the session {session.name}. It has {session.updates.Count} updates",
                LogType.LOG,
                0,
                null,
                "connection"
            );

            client.SendMessage(BinaryMessageType.SESSION_CREATE, session);
        };
        
        public static Connection.OnSessionLeaveHandler OnLeave(Connection client) => session =>
        {
            SessionManager.Instance.connections[session.id].Remove(client);
            client.SendMessage(BinaryMessageType.SESSION_LEAVE, (string)null);
        };
    }
}