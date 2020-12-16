namespace server.SessionListeners
{
    using OpenUp.Networking;
    using System;
    using System.Collections.Generic;

    public static class ReduxListeners
    {
        private static Dictionary<Guid, HashSet<Connection>> connections => SessionManager.Instance.connections;

        public static Session.OnReduxActionHandler ReduxHandler(Guid id) => act =>
        {
            foreach (Connection connection in connections[id])
            {
                connection.SendReduxAction(act);
            }
        };
    }
}