namespace server.DBConnection.SerializationTools
{
    using MongoDB.Bson.Serialization;
    using MongoDB.Bson.Serialization.Serializers;
    using UnityEngine;

    class ColorSerializer : SerializerBase<Color>, IBsonDocumentSerializer
    {
        public override Color Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
        {
            context.Reader.ReadStartArray();

            Color color = new Color(
                (float)context.Reader.ReadDouble(),
                (float)context.Reader.ReadDouble(),
                (float)context.Reader.ReadDouble(),
                (float)context.Reader.ReadDouble()
            );

            context.Reader.ReadEndArray();

            return color;
        }

        public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, Color color)
        {
            context.Writer.WriteStartArray();

            context.Writer.WriteDouble(color.r);
            context.Writer.WriteDouble(color.g);
            context.Writer.WriteDouble(color.b);
            context.Writer.WriteDouble(color.a);

            context.Writer.WriteEndArray();
        }

        public bool TryGetMemberSerializationInfo(string memberName, out BsonSerializationInfo serializationInfo)
        {
            serializationInfo = null;
            return true;
        }
    }
}
