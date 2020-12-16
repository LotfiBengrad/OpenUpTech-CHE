namespace server.SessionListeners
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using OpenUp.Networking;
    using OpenUp.Networking.LogMessages;
    using OpenUp.Networking.Players;
    using Utils;

    public static class PlayerListeners
    {
        public static Session.OnPlayerCreatedHandler OnPlayerCreated(Guid sessionID) => (player, connection) =>
        {
            ErrorLogger.LogMessage($"Player created for session {sessionID}: {player.name} (Guid: {player.id})", connection);
            SessionManager.Instance.RegisterPlayer(connection, player.id);

            IEnumerable<PlayerData> oldPlayers = SessionManager.Instance.GetSession(sessionID).players
                                                               .Select(kvp => kvp.Value)
                                                               .Where(pd => pd.deviceID == player.deviceID && player.id != pd.id);

            foreach(PlayerData oldPlayer in oldPlayers)
            {
                SessionManager.Instance.GetSession(sessionID).RemovePlayer(oldPlayer.id);
            }
        };


        public static Session.OnPlayerUpdatedHandler OnPlayerUpdated(Guid sessionID) => player =>
        {
            // Forward player information to all clients
            foreach (Connection connection in SessionManager.Instance.connections[sessionID])
            {
                connection.SendPlayerUpdate(player);
            }
        };
        
        public static Session.OnPlayerLeaveHandler OnPlayerLeave(Guid sessionID) => player =>
        {
            // Forward player leave to all clients
            foreach (Connection connection in SessionManager.Instance.connections[sessionID])
            {
                connection.SendMessage(BinaryMessageType.PLAYER_LEAVE, player.id);
            }
        };
    }
}
