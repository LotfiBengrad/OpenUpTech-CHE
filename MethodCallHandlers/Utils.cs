namespace server
{
    using OpenUp.Networking.LogMessages;
    using Utils;

    public partial class MethodCallHandlers
    {
        public void LogMessage(string message)
        {
            ErrorLogger.LogMessage(
                message,
                LogType.LOG,
                0,
                null,
                "Connection: "+ connection.id,
                connection.endPoint.ToString()
            );
        }
    }
}