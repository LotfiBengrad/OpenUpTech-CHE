namespace server.TCPTools
{
    using OpenUp.Networking;
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading.Tasks;
    using Utils;

    [Obsolete("Too buggy for iOS", true)]
    public static class TCP
    {
        public static bool isListening     = false;
        public static int  connectionCount = 0;
        public static Dictionary<IPAddress, Connection> expectedConnections = new Dictionary<IPAddress, Connection>();
        
        public static async Task StartListening()
        {
            try
            {
                await SetupTCP();
            }
            catch (Exception exception)
            {
                isListening = false;
                Console.WriteLine(exception);
                ErrorLogger.StoreError(new Exception("Failed to start TCP Test", exception));
            }
        }
        
        public static async Task SetupTCP()
        {
            // Local address is set to Any and not 127.0.0.1 because the EC2 instance
            // has an internal IPAddress different from its public address, and as such will
            // not respond to anything targeted at that public address if it listens to the local address.
            TcpListener tcp = new TcpListener(IPAddress.Any, 5005);
            
            tcp.Start();

            isListening = true;
            
            while (true)
            {
                TcpClient client = await tcp.AcceptTcpClientAsync();
                
                connectionCount++;
                IPAddress clientAddress = (client.Client.RemoteEndPoint as IPEndPoint)?.Address;
                
                if (clientAddress != null && expectedConnections.ContainsKey(clientAddress))
                {
                    Connection connection = expectedConnections[clientAddress];
                    // connection.sessionConnection = new SessionConnection(client);
                }
                else
                {
                    // Don't know this connection, so reject it
                    client.Close();
                }
            }
        }
    }
}