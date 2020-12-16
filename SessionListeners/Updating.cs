namespace server.SessionListeners
{
    using OpenUp.Networking;
    using OpenUp.Networking.LogMessages;
    using System;
    using System.Collections.Generic;

    public static class UpdateListeners
    {
        private static Dictionary<Guid, HashSet<Connection>> connections => SessionManager.Instance.connections;
        
        public static Session.OnUpdateHandler HandleAppUpdate(Guid id) => update =>
        {
            foreach (Connection connection in connections[id])
            {
                connection.SendAppUpdate(update);
            }
        };
        
        public static Session.OnInvalidUpdateHandler HandleInvalidUpdate(Guid sessionId) => exception =>
        {
            Console.WriteLine("That was not a valid update");
            Console.WriteLine(exception);

            Utils.ErrorLogger.StoreError(exception, SessionManager.Instance.GetSession(sessionId));
        };
    }
}