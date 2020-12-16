namespace server.DBConnection.SerializationTools
{
    using MongoDB.Bson.Serialization;
    using MongoDB.Bson.Serialization.Serializers;
    using OpenUp.DataStructures;
    using OpenUp.Utils;
    using System;
    using Utils;

    class TransformSerializer : SerializerBase<TransformStructure>, IBsonDocumentSerializer
    {
        public override TransformStructure Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
        {
            context.Reader.ReadStartDocument();
            
            byte[] data = context.Reader.ReadBinaryData().AsByteArray;

            BinaryUtils.ReadStruct(
                new ArraySegment<byte>(data),
                out TransformStructure transform
            );

            context.Reader.LeaveDocument();

            return transform;
        }

        public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, TransformStructure transform)
        {
            context.Writer.WriteStartDocument();

            byte[] data = new byte[TransformStructure.size];
            BinaryUtils.WriteStructToBytes(transform, data, 0);
                
            context.Writer.WriteName("data");
            context.Writer.WriteBinaryData(data);
                
            context.Writer.WriteEndDocument();
        }

        public bool TryGetMemberSerializationInfo(string memberName, out BsonSerializationInfo serializationInfo)
        {
            serializationInfo = null;
            return true;
        }
    }
}