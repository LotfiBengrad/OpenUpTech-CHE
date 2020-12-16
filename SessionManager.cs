namespace server
{
    using DBConnection;
    using OpenUp.DataStructures;
    using OpenUp.Networking;
    using OpenUp.Networking.ServerCalls;
    using SessionListeners;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Unity.Collections;
    using Utils;

    public class SessionManager : ISessionProvider
    {
        private static Lazy<SessionManager> _Instance = new Lazy<SessionManager>();
        public static SessionManager Instance => _Instance.Value;

        private readonly Dictionary<Guid, Session> sessions = new Dictionary<Guid, Session>();
        private readonly Dictionary<Guid, Connection> playerConnectionMap = new Dictionary<Guid, Connection>();
        public readonly Dictionary<Guid, HashSet<Connection>> connections = new Dictionary<Guid, HashSet<Connection>>();

        public IEnumerable<Session> allSessions => sessions.Values;

        private void AddSession(Session session)
        {
            sessions.Add(session.id, session);
            connections.Add(session.id, new HashSet<Connection>());

            session.OnUpdate            += UpdateListeners.HandleAppUpdate(session.id);
            session.OnInvalidUpdate     += UpdateListeners.HandleInvalidUpdate(session.id);
            session.OnReduxAction       += ReduxListeners.ReduxHandler(session.id);
            session.OnPlayerCreated     += PlayerListeners.OnPlayerCreated(session.id);
            session.OnPlayerUpdated     += PlayerListeners.OnPlayerUpdated(session.id);
            session.OnPlayerLeave       += PlayerListeners.OnPlayerLeave(session.id);
            session.OnPlayStateChange   += PlayStateListeners.OnPlayStateChange(session.id);
            session.OnVoiceDataReceived += VoiceListeners.OnVoiceDataReceived(session.id);

            session.ownershipRegistry.OnOwnershipRequested += OwnershipListeners.OnRequest(session);
            session.ownershipRegistry.OnOwnershipGranted   += OwnershipListeners.OnGranted(session);
            session.ownershipRegistry.OnOwnershipDenied    += OwnershipListeners.OnDenied(session);
        }

        public Session GetSession(Guid id)
        {
            if (sessions.ContainsKey(id)) return sessions[id];

            throw new Exception($"Attempted to get session with unknown id: {id.ToString()}");
        }

        public List<Session> AllSessionsForApp(string appId)
        {
            return sessions.Values
                .Where(s => s.app.id == appId)
                .ToList();
        }

        public Session CreateSession(ArraySegment<byte> data)
        {
            Session session = new Session(data);

            AddSession(session);

            return session;
        }

        public Session CreateSession(SessionOptions options, UserInfo requestingUser)
        {
            AppStructure app;
            if (options.appId != null)
            {
                app = MongoConnection.Instance.GetApp(options.appId);
            }
            else
            {
                app = new AppStructure
                {
                    id = Guid.NewGuid().ToString(),
                    name = NameGenerator.GenerateRandomName(3),
                };

                // app.mainStory.setups.Add("start", StorySetup.StartNode(app.mainStory.itemPath));
            }

            Console.WriteLine($"Found app has {app.objects.Count} objects");

            Session session = new Session
            {
                baseApp = app.ToBytes(),
                app = app,
                name = options.name ?? $"{requestingUser?.name ?? "Guest"} plays {app.name}" ,
                uiHistory = options.uiHistory
            };

            Console.WriteLine($"Adding session with {session.uiHistory.Count} redux actions");

            AddSession(session);

            return session;
        }

        public void RegisterPlayer(Connection connection, Guid player)
        {
            playerConnectionMap[player] = connection;
        }

        public Guid PlayerForConnection(Guid connection)
        {
            return playerConnectionMap.FirstOrDefault(kvp => kvp.Value.id == connection)
                                      .Key;
        }

        public Guid PlayerForConnection(Connection connection)
        {
            return PlayerForConnection(connection.id);
        }

        public Connection ConnectionForPlayer(Guid playerId)
        {
            return playerConnectionMap[playerId];
        }
    }
}
