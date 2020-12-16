namespace server
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using DBConnection;
    using MongoDB.Bson;
    using MongoDB.Driver;
    using OpenUp.DataStructures;
    using OpenUp.Networking;
    using OpenUp.Networking.ServerCalls;
    using OpenUp.Updating.Templating;
    using System;
    using System.Linq;

    public partial class MethodCallHandlers : IServerCallMethods
    {
        private readonly Connection connection;
        public MethodCallHandlers(Connection connection)
        {
            this.connection = connection;
        }
        
        public async Task<List<AppListItem>> ListAllApps()
        {
            return await MongoConnection.Instance.ListApps();
        }

        public async Task<List<SessionListItem>> ListSessions(string id)
        {
            return SessionManager.Instance
                .AllSessionsForApp(id)
                .Select(s => new SessionListItem
                {
                    id = s.id,
                    name = s.name,
                    participantCount = s.players.Count
                })
                .ToList();
        }

        public async Task<List<SessionListItem>> ListActiveSessions()
        {
            return SessionManager.Instance
                                 .allSessions
                                 .Where(s => s.players.Count > 0)
                                 .Select(s => new SessionListItem
                                 {
                                     id               = s.id,
                                     name             = s.name,
                                     participantCount = s.players.Count
                                 })
                                 .ToList();
        }

        public async Task<Exception> SaveApp()
        {
            if (connection.session == null) return new Exception("No current session to save app.");
            
            Exception e = await MongoConnection.Instance.SaveApp(connection.session.app);

            if (e != null)
            {
                Console.WriteLine(e);
            }
            
            return e;
        }

        public async Task<List<Template>> ListTemplates()
        {
            IAsyncCursor<Template> find = await MongoConnection.Instance.templates.FindAsync(new BsonDocumentFilterDefinition<Template>(new BsonDocument()));
            
            return find.ToList();
        }

        public async Task CreateTemplate(Template template)
        {
            Console.WriteLine("Inserting a template");
            Console.WriteLine(template.name);
            
            await MongoConnection.Instance.templates.InsertOneAsync(template);
            
            LogMessage(
                $"Inserted Template: {template.name}\n"
                + $"{template.dataUpdate}"
            );
        }
    }
}