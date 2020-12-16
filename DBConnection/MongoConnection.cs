namespace server.DBConnection
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Threading.Tasks;

    using MongoDB.Bson;
    using MongoDB.Driver;

    using BCrypt.Net;
    using Microsoft.VisualBasic.CompilerServices;
    using OpenUp.DataStructures;
    using OpenUp.Networking.LogMessages;
    using OpenUp.Networking.ServerCalls;
    using OpenUp.Updating.Templating;
    using OpenUp.DataStructures.LDraw;
    using OpenUp.DataStructures.SceneShots;
    using System.Linq.Expressions;
    using Utils;

    public class MongoConnection
    {
        private const string DB_USERNAME = "PositionSyncUser";
        private const string DB_PASSWORD = "pI7KilwhPNAJTRZ0";

        private const string SERVER_URL = "mongodb+srv://"
                                         +DB_USERNAME+":"+DB_PASSWORD+
                                          "@voice-testing.yxk7t.mongodb.net/Position-Sync?retryWrites=true&w=majority";

        private const string DB_NAME = "Position-Sync";

        private static readonly Lazy<MongoConnection> _Instance = new Lazy<MongoConnection>();

        public static MongoConnection Instance => _Instance.Value;

        private MongoClient client;
        private IMongoDatabase database;

        internal IMongoCollection<AppStructure> apps {get; private set;}
        internal IMongoCollection<User> users {get; private set;}
        internal IMongoCollection<Organisation> organisations {get; private set;}
        internal IMongoCollection<StoredException> exceptions {get; private set;}
        internal IMongoCollection<FullLogs> appLogs {get; private set;}
        internal IMongoCollection<ServerLogMessage> serverLogs {get; private set;}
        internal IMongoCollection<Template> templates {get; private set;}
        internal IMongoCollection<ShapeRef> shapes    {get; private set;}
        internal IMongoCollection<LDrawModel> LDrawStandardModels {get; private set;}
        internal IMongoCollection<LDrawModel> LDrawCustomModels {get; private set;}
        internal IMongoCollection<SceneShot> sceneShots {get; private set;}

        public MongoConnection()
        {
            AppStorageMap.InitMapping();
            User.AddMappings();

            MongoUrl url = new MongoUrl(SERVER_URL);

            client     = new MongoClient(url);
            database   = client.GetDatabase(DB_NAME);

            apps                = database.GetCollection<AppStructure>("apps");
            users               = database.GetCollection<User>("users");
            organisations       = database.GetCollection<Organisation>("organisations");
            exceptions          = database.GetCollection<StoredException>("server_errors");
            appLogs             = database.GetCollection<FullLogs>("app_logs");
            serverLogs          = database.GetCollection<ServerLogMessage>("server_logs");
            shapes              = database.GetCollection<ShapeRef>("shapes");
        }

        public async Task<List<AppListItem>> ListApps()
        {
            IAsyncCursor<AppStructure> allApps = await apps.FindAsync(new BsonDocument());

            List<AppStructure> appList = await allApps.ToListAsync();

            return appList.Select(app => new AppListItem {
                id     = app.id,
                name   = app.name,
                author = new UserInfo
                {
                    id = app.tenant,
                    name = "Important"
                }
            }).ToList();
        }

        public AppStructure GetApp(string appId)
        {
            return apps.FindSync(ap => ap.id == appId).First();
        }

        public async Task<Exception> SaveApp(AppStructure app)
        {
            try
            {
                Expression<Func<AppStructure, bool>> findExisting = ap => ap.id == app.id;

                long existing = await apps.CountDocumentsAsync(findExisting);

                if (existing == 0)
                {
                    await apps.InsertOneAsync(app);
                }
                else
                {
                    await apps.ReplaceOneAsync(findExisting, app);
                }
                return null;
            }
            catch (Exception exception)
            {
                return exception;
            }
        }

        private FilterDefinition<User> GetUserFilter(string usernameOrEmail)
        {
            FilterDefinition<User> filterID  = Builders<User>.Filter.Eq("id", usernameOrEmail);
            FilterDefinition<User> filterName  = Builders<User>.Filter.Eq("username", usernameOrEmail);
            FilterDefinition<User> filterEmail = Builders<User>.Filter.Where(
                user => user.emails.Any(email => email.address == usernameOrEmail)
            );

            return Builders<User>.Filter.Or(filterID, filterName, filterEmail);
        }

        internal async Task<User> GetUser(string usernameOrEmail)
        {
            IAsyncCursor<User> user = await users.FindAsync<User>(GetUserFilter(usernameOrEmail));

            return user.FirstOrDefault();
        }

        public async Task<(string token, UserInfo user)> LogIn(string username, string passSHA256)
        {
            User user = await GetUser(username);

            if (user == null)
            {
                throw LoginException.UnknownUser(username);
            }

            string bcryptHash = user.services.password.bcrypt;

            if (bcryptHash == null) throw LoginException.UnknownUser(username);

            bool isvalid = BCrypt.Verify(passSHA256, bcryptHash);

            if (!isvalid) throw LoginException.PasswordIncorrect();

            byte[] bytes = new byte[64];

            using (RNGCryptoServiceProvider byteGen = new RNGCryptoServiceProvider())
            {
                byteGen.GetBytes(bytes);
            }

            string resumeToken = Convert.ToBase64String(bytes);

            User.ResumeToken newToken = new User.ResumeToken {
                token   = resumeToken,
                expires = DateTime.Now.AddDays(90)
            };

            FilterDefinition<User> filter = GetUserFilter(user.username);

            FieldDefinition<User>  loginTokens = new StringFieldDefinition<User>("services.resume.loginTokens");
            UpdateDefinition<User> update   = Builders<User>.Update.Push(loginTokens, newToken);

            await users.UpdateOneAsync(filter, update);

            return (resumeToken, new UserInfo { id = user.id, name = user.username });
        }

        public async Task<(string token, UserInfo user)> LogIn(string token)
        {
            User user = users.AsQueryable()
                             .FirstOrDefault(
                                 doc => doc.services.resume.loginTokens.Any(t => t.token == token)
                             );

            if (user == null) throw LoginException.InvalidToken();

            User.ResumeToken oldToken = user.services.resume.loginTokens.Find(t => t.token == token);

            if (oldToken.expires < DateTime.Now) throw LoginException.TokenExpired();

            byte[] bytes = new byte[64];

            using (RNGCryptoServiceProvider byteGen = new RNGCryptoServiceProvider())
            {
                byteGen.GetBytes(bytes);
            }

            string resumeToken = Convert.ToBase64String(bytes);

            User.ResumeToken newToken = new User.ResumeToken {
                token   = resumeToken,
                expires = DateTime.Now.AddDays(90)
            };

            FilterDefinition<User> filter = GetUserFilter(user.username);
            FieldDefinition<User>  loginTokens = new StringFieldDefinition<User>("services.resume.loginTokens");
            UpdateDefinition<User> updateNew   = Builders<User>.Update.Push(loginTokens, newToken);
            UpdateDefinition<User> updateOld   = Builders<User>.Update.Pull(loginTokens, oldToken);

            await users.UpdateOneAsync(filter, updateOld);
            await users.UpdateOneAsync(filter, updateNew);

            return (resumeToken, new UserInfo { id = user.id, name = user.username });
        }

        public async Task<bool> IsValidToken(string userID, string token)
        {
            IAsyncCursor<User> userFetch = await users.FindAsync<User>(Builders<User>.Filter.Eq("id", userID));

            User user = userFetch.FirstOrDefault();

            if (user == null)
            {
                throw LoginException.UnknownUser(userID);
            }

            User.ResumeToken resume = user.services.resume.loginTokens.FirstOrDefault(t => t.token == token);

            if (resume == null)
            {
                throw LoginException.InvalidToken();
            }

            if (resume.expires < DateTime.Now)
            {
                throw LoginException.TokenExpired();
            }

            return true;
        }
    }
}
