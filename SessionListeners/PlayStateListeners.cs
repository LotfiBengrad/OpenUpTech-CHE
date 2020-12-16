namespace server.SessionListeners
{
    using OpenUp.Networking;
    using OpenUp.Utils;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using OpenUp.Updating;
    using server.Utils;
    using OpenUp.Networking.LogMessages;

    public static class PlayStateListeners
    {
        public static Session.OnPlayStateChangeHandler OnPlayStateChange(Guid sessionID) => (state, sourceConnection) =>
        {
            if (state == PlayState.PLAYING)
            {
                sourceConnection.session.ownershipRegistry[new UpdatePath(new List<string>())] = SessionManager.Instance.PlayerForConnection(sourceConnection);
                sourceConnection.session.ownershipRegistry.mainOwner = SessionManager.Instance.PlayerForConnection(sourceConnection);

                sourceConnection.GrantOwnership(
                    new UpdatePath(new List<string>()), 
                    SessionManager.Instance.PlayerForConnection(sourceConnection)
                );

                ErrorLogger.LogMessage($"Playing app {sourceConnection.session.app.name} with as main owner (Guid: {SessionManager.Instance.PlayerForConnection(sourceConnection)})", sourceConnection);
            }
            else if (state != PlayState.PLAYING && state != PlayState.PAUSED)
            {
                sourceConnection.session.ownershipRegistry.Clear();
            }

            // Forward player information to all clients
            foreach (Connection connection in SessionManager.Instance.connections[sessionID])
            {
                connection.SendPlayStateChange(state);
            }
        };
    }
}