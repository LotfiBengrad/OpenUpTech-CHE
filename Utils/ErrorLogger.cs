namespace server.Utils
{
    using DBConnection;
    using MongoDB.Bson.Serialization.Attributes;
    using OpenUp.Networking;
    using OpenUp.Networking.LogMessages;
    using System;
    using System.Threading.Tasks;
    using UnityEngine;
    using LogType = OpenUp.Networking.LogMessages.LogType;

    public class StoredException
    {
        [BsonId]
        public string id { get; set; }

        [BsonElement("timestamp")]
        public DateTime expires { get; set; }

        [BsonElement("message")]
        public string message { get; set; }

        [BsonElement("stacktrace")]
        public string stack { get; set; }

        [BsonElement("source")]
        public string source { get; set; }
    }

    public static class ErrorLogger
    {
        public const string UNKNOWN_SOURCE = "unknown";
        public const string SELF_SOURCE = "internal";

        public static void TrackErrors(Connection connection)
        {
            connection.OnMessageException = exception =>
            {
                Console.WriteLine($"Something went wrong handling server message: \n{exception}");
                StoreError(exception, connection);
            };
        }

        public static void StoreError(Exception exception, object context = null)
        {
            ServerLogMessage exp = new ServerLogMessage {
                id        = Guid.NewGuid().ToString(),
                message   = new LogMessage
                {
                    message = $"{exception.GetType()}: {exception.Message}",
                    logType = LogType.EXCEPTION,
                    stacktrace = exception.StackTrace
                },
                timeStamp = DateTime.Now.ToUniversalTime(),
            };

            if (context == null)
            {
                exp.source = SELF_SOURCE;
                exp.sourceIP = null;
            }
            else if (context is Connection connection)
            {
                exp.source   = $"Connection: {connection.id}";
                exp.sourceIP = connection.endPoint.ToString();
            }
            else if (context is Session sess)
            {
                exp.source = $"Session: {sess.name} ({sess.id}) (State = {sess.state})";
                exp.sourceIP = null;
            }
            else
            {
                exp.source = UNKNOWN_SOURCE;
            }
            
            Task.Run(() => SendToDB(exp));
        }

        public static void LogMessageObject(BaseLogMessage logMessage, string source = SELF_SOURCE, string sourceIP = null)
        {
            // Create ServerLogMessage object
            ServerLogMessage serverLogMessage = new ServerLogMessage {
                id = Guid.NewGuid().ToString(),
                message = logMessage,
                timeStamp = DateTime.Now.ToUniversalTime(),
                source = source,
                sourceIP = sourceIP
            };

            // Log message to console
            Console.WriteLine(
                "--------------------\n" +
                $"[{serverLogMessage.timeStamp.ToLongTimeString()}] {logMessage}"
            );

            // Send log to database
            Task.Run(() => SendToDB(serverLogMessage));
        }

        public static void LogMessage(
            string message,
            LogType logType,
            float timeStamp,
            string stacktrace,
            string source = SELF_SOURCE,
            string sourceIP = null
        )
        {
            LogMessageObject(
                new LogMessage {
                    message = message,
                    logType = logType,
                    timeStamp = timeStamp,
                    stacktrace = stacktrace
                },
                source,
                sourceIP
            );
        }

        public static void LogMessage(string message, object context = null)
        {
            LogMessageObject(
                new LogMessage
                {
                    message = message,
                    logType = LogType.LOG,
                    timeStamp = 0,
                    stacktrace = null
                },
                ContextToString(context),
                ContextToIPString(context)
            );
        }

        public static void MetricMessage(
            float timeStamp,
            string methodName,
            long callAmount,
            float timeTaken,
            string source = SELF_SOURCE,
            string sourceIP = null
        )
        {
            LogMessageObject(
                new MetricMessage {
                    timeStamp = timeStamp,
                    methodName = methodName,
                    callAmount = callAmount,
                    timeTaken = timeTaken
                },
                source,
                sourceIP
            );
        }

        public static void PerformanceMessage(
            string message,
            float timeStamp,
            string details,
            string source = SELF_SOURCE,
            string sourceIP = null
        )
        {
            LogMessageObject(
                new PerformanceMessage {
                    message = message,
                    timeStamp = timeStamp,
                    details = details
                },
                source,
                sourceIP
            );
        }

        private static async void SendToDB(StoredException exp)
        {
            await MongoConnection.Instance.exceptions.InsertOneAsync(exp);
        }

        private static async void SendToDB(ServerLogMessage logMessage)
        {
            await MongoConnection.Instance.serverLogs.InsertOneAsync(logMessage);
        }

        private static string ContextToString(object context)
        {
            switch(context)
            {
                case null:
                    return SELF_SOURCE;

                case Connection connection:
                    return $"Connection: {connection.id}";

                case Session sess:
                    return $"Session: {sess.name} ({sess.id}) (State = {sess.state})";

                default:
                    return UNKNOWN_SOURCE;
            }
        }

        private static string ContextToIPString(object context)
        {
            if (context == null)
            {
                return SELF_SOURCE;
            }
            else if (context is Connection connection)
            {
                return connection.endPoint.ToString();
            }
            else if (context is Session sess)
            {
                return "SESSION";
            }
            else
            {
                return UNKNOWN_SOURCE;
            }
        }
    }
}
