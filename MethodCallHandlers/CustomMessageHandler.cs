namespace server
{
    using System;
    using System.Threading.Tasks;
    using OpenUp.DataStructures;
    using OpenUp.Networking.ServerCalls;
    using OpenUp.Utils;
    using OpenUpDataStructures.Extentions;
    using Utils;

    public partial class MethodCallHandlers : IServerCallMethods
    {
        public Task<byte[]> SendCustomMessage(string messageType, byte[] data)
        {
            switch (messageType)
            {
                case "testMessage":
                    return HandleTestMessage(data);
                    
                default: 
                    throw new ArgumentOutOfRangeException(nameof(messageType), $"Cannot handle custom message type: {messageType}");
            }
        }

        private async Task<byte[]> HandleTestMessage(byte[] _data)
        {
            ArraySegment<byte> data = new ArraySegment<byte>(_data);
            
            int idx = 0;
            idx += BinaryUtils.ReadString(data.Slice(idx), out string name);
            idx += BinaryUtils.ReadStruct(data.Slice(idx), out TransformStructure trans1);
            idx += BinaryUtils.ReadStruct(data.Slice(idx), out TransformStructure trans2);
            
            // do something with this data
            ErrorLogger.LogMessage($"Name: {name} \n" 
                                  + $"Transform One: \n {trans1}" 
                                  + $"Transform Two: \n {trans2}"
                                   ,connection
                );

            const string res = "Thank you very much.";

            return res.ToBytes();
        }
    }
}