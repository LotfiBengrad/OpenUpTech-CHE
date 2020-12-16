namespace server.SessionListeners
{
    using OpenUp.Networking;
    using OpenUp.Updating;
    using System;

    public static class OwnershipListeners
    {
        public static OwnershipRegistry.OnOwnershipRequestedHandler OnRequest(Session session) =>
            (UpdatePath path, Guid playerId) =>
            {
                Guid currentOwner = session.ownershipRegistry.OwnerOf(path);
                Connection connection = SessionManager.Instance.ConnectionForPlayer(currentOwner);
                connection.RequestOwnership(path, playerId);
            };
        
        public static OwnershipRegistry.OnOwnershipGrantedHandler OnGranted(Session session) =>
            (UpdatePath path, Guid playerId) =>
            {
                Connection connection = SessionManager.Instance.ConnectionForPlayer(playerId);
                connection.GrantOwnership(path, playerId);
            };
    
        public static OwnershipRegistry.OnOwnershipDeniedHandler OnDenied(Session session) =>
            (UpdatePath path, Guid playerId) =>
            {
                Connection connection = SessionManager.Instance.ConnectionForPlayer(playerId);
                connection.DenyOwnership(path, playerId);
            };
    }
}