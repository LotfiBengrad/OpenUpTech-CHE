namespace server
{
    using MongoDB.Bson;
    using MongoDB.Driver;
    using OpenUp.Networking.LogMessages;
    using OpenUp.Networking.Players;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using DBConnection;

    public partial class MethodCallHandlers
    {
        public async Task<List<FullLogsMetadata>> GetAllLogs()
        {
            // Use thread-safe collection as we use an async operation to add to it.
            ConcurrentQueue<FullLogsMetadata> data = new ConcurrentQueue<FullLogsMetadata>();

            IAsyncCursor<FullLogs> find = await DBConnection.MongoConnection.Instance.appLogs.FindAsync<FullLogs>(new BsonDocument(), new FindOptions<FullLogs>
            {
                Projection = new BsonDocumentProjectionDefinition<FullLogs, FullLogs>(new BsonDocument
                {
                    ["metadata"] = 1
                })

            }, CancellationToken.None);

            await find.ForEachAsync(full => data.Enqueue(full.metadata));

            List<FullLogsMetadata> list = new List<FullLogsMetadata>(data);

            return list;
        }

        public async Task<FullLogs> GetLogsById(string id)
        {
            IAsyncCursor<FullLogs> find = await DBConnection.MongoConnection.Instance.appLogs.FindAsync(full => full.id == id);

            FullLogs logs = find.FirstOrDefault();

            if (logs == null) throw new Exception("I will fail to find log with id: "+id);

            return logs;
        }

        public async Task SendLogs(List<BaseLogMessage> logs)
        {
            Console.WriteLine("Received these logs");
            FullLogsMetadata metadata = new FullLogsMetadata
                {
                    id = Guid.NewGuid().ToString(),
                    timestamp = DateTime.Now,
                    sessionName = connection.session?.name,
                    playerData = connection.session?.players.Values.First() ?? new PlayerData(),
                    user = connection.user
                };

            FullLogs fullLogs = new FullLogs(metadata, logs);

            fullLogs.id = metadata.id;

            await MongoConnection.Instance.appLogs.InsertOneAsync(fullLogs);
        }

        public async Task<List<ServerLogMessage>> GetServerLogs(DateTime startDate, DateTime endDate)
        {
            FilterDefinition<ServerLogMessage> startDateFilter = Builders<ServerLogMessage>.Filter.Gte("timeStamp", startDate);
            FilterDefinition<ServerLogMessage> endDateFilter = Builders<ServerLogMessage>.Filter.Lte("timeStamp", endDate);

            // Apply one or both filters depending on whether an end date was supplied
            FilterDefinition<ServerLogMessage> filter;
            if (endDate == default)
            {
                filter = startDateFilter;
                Console.WriteLine($"Client requested server logs since {startDate.ToLocalTime().ToString()}");
            }
            else
            {
                filter = Builders<ServerLogMessage>.Filter.And(startDateFilter, endDateFilter);
                Console.WriteLine($"Client requested server logs between {startDate.ToLocalTime().ToString()} and {endDate.ToLocalTime().ToString()}");
            }

            // Get logs from server
            List<ServerLogMessage> serverLogMessages = MongoConnection.Instance.serverLogs.Find(filter).ToList();

            return serverLogMessages;
        }

        public async Task PreserveClientLog(string id, bool preserve)
        {
            FilterDefinition<FullLogs> filter = Builders<FullLogs>.Filter.Eq("id", id);
            UpdateDefinition<FullLogs> update = Builders<FullLogs>.Update.Set("metadata.preserved", preserve);
            await MongoConnection.Instance.appLogs.UpdateOneAsync(filter, update);
            Console.WriteLine($"Now preserving app_log with id '{id}': {preserve}");
        }
    }
}
