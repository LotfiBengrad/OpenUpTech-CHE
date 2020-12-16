namespace server.SessionListeners
{
    using System;
    using OpenUp.Networking;
    using server.Utils;

    public static class VoiceListeners
    {
        public static Session.OnVoiceDataReceivedHandler OnVoiceDataReceived(Guid sessionID) => (playerID, voiceBytes) =>
        {
            Console.WriteLine($"Voice message received in session '{sessionID}' - {voiceBytes.Length} bytes:");
            Console.WriteLine(BitConverter.ToString(voiceBytes));

            foreach (Connection sessionClient in SessionManager.Instance.connections[sessionID])
            {
                ErrorLogger.LogMessage($"Voice message from {playerID} bounced to client {sessionClient.id}", SessionManager.Instance.GetSession(sessionID));
                
                //don't send to self
                if (SessionManager.Instance.PlayerForConnection(sessionClient) == playerID) continue;

                sessionClient.SendVoiceMessage(playerID, voiceBytes);
            }
        };
    }
}
