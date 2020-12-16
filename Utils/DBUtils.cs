using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using server.DBConnection;
using MongoDB.Driver;
using OpenUp.Networking.ServerCalls;
using OpenUp.Networking.LogMessages;

namespace server.Utils
{
    using MongoDB.Bson;
    using OpenUp.DataStructures;
    using System.Linq;

    public static class DBUtils
    {
        public static void UseShapeRef(ShapeRef shapeRef)
        {
            if (shapeRef.type == ShapeType.VISUAL)
            {
                VisualizationType t = (VisualizationType) shapeRef.enumKey;

                VisualTypeMetaData meta = new VisualTypeMetaData
                {
                    shape       = shapeRef.shape,
                    displayName = shapeRef.name
                };

                VisualizationCollection.Instance[t] = meta;
            }
            else
            if (shapeRef.type == ShapeType.BEHAVIOUR)
            {
                BehaviourType t = (BehaviourType) shapeRef.enumKey;

                BehaviourMetaData meta = new BehaviourMetaData
                {
                    shape = shapeRef.shape,
                    name  = shapeRef.name
                };

                BehaviourCollection.Instance[t] = meta;
            }
        }

        public static async Task SetShapesOnStartUp()
        {
            IAsyncCursor<ShapeRef> shp = await MongoConnection.Instance.shapes.FindAsync(new BsonDocument());
            List<ShapeRef> shapes = await shp.ToListAsync();

            foreach (ShapeRef shapeRef in shapes)
            {
                UseShapeRef(shapeRef);
            }
        }

        public static async Task InsertShape(ShapeRef shape)
        {
            IAsyncCursor<ShapeRef> shp = await MongoConnection.Instance.shapes.FindAsync(Builders<ShapeRef>.Filter.Eq("name", shape.name));

            ShapeRef found = shp.First();

            VisualTypeMetaData meta = new VisualTypeMetaData
            {
                shape = found.shape,
                displayName = found.name,
                visualType = (VisualizationType) found.enumKey
            };

            VisualizationCollection.Instance[meta.visualType] = meta;
        }

        public static async Task TestUserMapping() {
            Console.WriteLine("TestUserMapping started");

            IMongoCollection<User> users = MongoConnection.Instance.users;

            FilterDefinition<User> filter = Builders<User>.Filter.Exists("profile.recentAssets");

            IAsyncCursor<User> userResult = await users.FindAsync(filter);
            List<User> userList = await userResult.ToListAsync();

            Console.WriteLine($"Users found: {userList.Count}");

            int maxAssets = 3;
            int maxSessions = 2;
            int maxRecentAssetsPerSession = 3;

            foreach (User u in userList)
            {
                Console.WriteLine("===============");
                Console.WriteLine($"User: {u.username}\n");
                Console.WriteLine($"Assets ({u.profile.assets.Count}):\n");
                Console.WriteLine($"This many recent objects: {u.profile.recentAssets.Count}");
                int assetCount = 0;
                foreach (Asset asset in u.profile.assets)
                {
                    logAsset(asset);

                    if (++assetCount == maxAssets)
                        break;
                }

                int sessionCount = 0;
                foreach (KeyValuePair<string, RecentAssets> session in u.profile.recentAssets)
                {
                    Console.WriteLine("---------------");
                    Console.WriteLine($"\nSession ID: {session.Key}");
                    Console.WriteLine($"Last Updated: {session.Value.lastUpdated}");
                    Console.WriteLine($"recentAssets: {session.Value.usedAssets.Count}\n");

                    int i = 0;
                    foreach (Asset asset in session.Value.usedAssets)
                    {
                        logAsset(asset);

                        if (++i == maxRecentAssetsPerSession)
                            break;
                    }

                    if (++sessionCount == maxSessions)
                        break;
                }
            }

            Console.WriteLine("===============");
        }

        private static void logAsset(Asset asset)
        {
            Console.WriteLine($"id: {asset.id}");
            Console.WriteLine($"name: {asset.name}");
            Console.WriteLine($"type: {asset.type}");
            Console.WriteLine($"authorName: {asset.authorName}");
            Console.WriteLine($"mainModelFile: {asset.mainModelFile}");
            Console.WriteLine($"thumbnail: {asset.thumbnail}");
            Console.WriteLine();
        }

        public static class CleanUp
        {
            private static List<Func<Task>> tasks = new List<Func<Task>>();

            private static bool started = false;

            public static void addRoutine(Func<Task> cleanup)
            {
                CleanUp.tasks.Add(cleanup);
            }

            // Starts the cleanup timer, running runCleanup() every X seconds
            public static void start(TimeSpan interval)
            {
                // Throw exception when started more than once
                if (CleanUp.started)
                    throw new InvalidOperationException("CleanUp has already been started. CleanUp.start() should only be called once.");

                // Start the timer
                Timer timer = new Timer();
                timer.Elapsed += (sender, args) => CleanUp.runCleanup();
                timer.AutoReset = true;
                timer.Interval = interval.TotalMilliseconds;
                timer.Start();

                CleanUp.started = true;
                Console.WriteLine($"CleanUp routine started, running every {interval.ToString()}.");
            }

            // Executes all scheduled tasks in turn
            private static async void runCleanup()
            {
                Console.WriteLine($"[{DateTime.Now.ToLongTimeString()}] Running scheduled cleanup...");
                foreach (Func<Task> task in CleanUp.tasks)
                    await task();
            }
        }

        public static async Task cleanupOutdatedRecentAssets()
        {
            Console.WriteLine("Removing outdated profile.recentAssets entries from the database...");

            IMongoCollection<User> users = MongoConnection.Instance.users;

            DateTime expirationDate = DateTime.Now.Subtract(new TimeSpan(7, 0, 0, 0));

            // Filter users with a non-empty profile.recentAssets
            FilterDefinition<User> filter = Builders<User>.Filter.And(
                Builders<User>.Filter.Exists("profile.recentAssets"),
                Builders<User>.Filter.Ne("profile.recentAssets", new BsonDocument())
            );

            IAsyncCursor<User> userResult = await users.FindAsync(filter);
            List<User> userList = await userResult.ToListAsync();

            Console.WriteLine();
            int cleanedUsers = 0;
            foreach (User user in userList)
            {
                // Filter out outdated apps
                Dictionary<string, RecentAssets> filteredRecentAssets = user.profile.recentAssets.Where(
                    app => app.Value.lastUpdated >= expirationDate
                ).ToDictionary(app => app.Key, app => app.Value);

                int cleanCount = user.profile.recentAssets.Count - filteredRecentAssets.Count;
                if (cleanCount > 0)
                {
                    // Write cleaned recentAssets list to the database
                    FilterDefinition<User> updatefilter = Builders<User>.Filter.Eq("_id", user.id);
                    UpdateDefinition<User> update = Builders<User>.Update.Set(
                        "profile.recentAssets",
                        filteredRecentAssets
                    );
                    await MongoConnection.Instance.users.UpdateOneAsync(updatefilter, update);

                    // Log cleaned user
                    cleanedUsers++;
                    Console.WriteLine($"Cleaned up {cleanCount} outdated app(s) for user '{user.username}'");
                }
            }
            Console.WriteLine($"\nCleaned {cleanedUsers} user(s)\n");
        }

        public static async Task cleanupExpiredOrganisationInvites()
        {
            Console.WriteLine("Marking old organisations.invites entries as expired...");

            IMongoCollection<Organisation> organisations = MongoConnection.Instance.organisations;

            DateTime expirationDate = DateTime.Now.Subtract(new TimeSpan(7, 0, 0, 0));

            // Empty filter as we want to apply the update to all organisations
            FilterDefinition<Organisation> filter = Builders<Organisation>.Filter.Empty;

            // Set the status of the filtered invites to expired
            UpdateDefinition<Organisation> update = Builders<Organisation>.Update.Set(
                "invites.$[expired].status",
                Organisation.Invite.Status.EXPIRED.ToString()
            );

            // Define array filters as BSON objects first
            BsonDocument arrayFilterBson = new BsonDocument(
                "expired.status", new BsonDocument(
                    "$eq", Organisation.Invite.Status.PENDING.ToString()
                )
            );
            arrayFilterBson.Add(
                "expired.date", new BsonDocument(
                    "$lt", expirationDate
                )
            );

            // Convert BSON to filter array
            BsonDocumentArrayFilterDefinition<Organisation.Invite> arrayFilter = new BsonDocumentArrayFilterDefinition<Organisation.Invite>(arrayFilterBson);
            UpdateOptions updateOptions = new UpdateOptions { ArrayFilters = new ArrayFilterDefinition[] { arrayFilter } };

            // Execute update query
            UpdateResult result = await organisations.UpdateManyAsync(filter, update, updateOptions);
            Console.WriteLine($"Updated {result.ModifiedCount} organisations with expired invites.\n");
        }

        public static async Task cleanupOutdatedAppLogs()
        {
            Console.WriteLine("Removing outdated, non-preserved app logs from the database...");

            IMongoCollection<FullLogs> logs = MongoConnection.Instance.appLogs;

            // Expiration date in UTC, as that is how MongoDB stores dates
            DateTime expirationDate = DateTime.Now.Subtract(new TimeSpan(7, 0, 0, 0)).ToUniversalTime();

            // Filter to select documents for deletion
            FilterDefinition<FullLogs> filter = Builders<FullLogs>.Filter.And(
                // Logs that are older than the expiration date
                Builders<FullLogs>.Filter.Lt("metadata.timestamp", expirationDate),

                // And logs that ...
                Builders<FullLogs>.Filter.Or(
                    // Either don't have a preserve field (accounts for old documents)
                    Builders<FullLogs>.Filter.Exists("metadata.preserved", false),

                    // Or which have the preserve field set to false
                    Builders<FullLogs>.Filter.Eq("metadata.preserved", false)
                )
            );

            DeleteResult result = await logs.DeleteManyAsync(filter);
            Console.WriteLine($"Cleaned {result.DeletedCount} app logs.\n");
        }
    }
}
