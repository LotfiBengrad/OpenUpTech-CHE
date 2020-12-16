namespace server.DBConnection.SerializationTools
{
    using MongoDB.Bson.Serialization;
    using MongoDB.Bson.Serialization.Serializers;
    using UnityEngine;

    class Vector3Serializer : SerializerBase<Vector3>, IBsonDocumentSerializer
    {
        public override Vector3 Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
        {
            context.Reader.ReadStartArray();

            Vector3 vector = new Vector3(
                (float)context.Reader.ReadDouble(),
                (float)context.Reader.ReadDouble(),
                (float)context.Reader.ReadDouble()
            );

            context.Reader.ReadEndArray();

            return vector;
        }

        public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, Vector3 vector)
        {
            context.Writer.WriteStartArray();

            context.Writer.WriteDouble(vector.x);
            context.Writer.WriteDouble(vector.y);
            context.Writer.WriteDouble(vector.z);

            context.Writer.WriteEndArray();
        }

        public bool TryGetMemberSerializationInfo(string memberName, out BsonSerializationInfo serializationInfo)
        {
            serializationInfo = null;
            return true;
        }
    }
}
