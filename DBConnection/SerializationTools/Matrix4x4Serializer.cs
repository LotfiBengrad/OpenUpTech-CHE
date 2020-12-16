namespace server.DBConnection.SerializationTools
{
    using MongoDB.Bson.Serialization;
    using MongoDB.Bson.Serialization.Serializers;
    using UnityEngine;

    class Matrix4x4Serializer : SerializerBase<Matrix4x4>, IBsonDocumentSerializer
    {
        public override Matrix4x4 Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
        {
            context.Reader.ReadStartArray();

            Vector4[] columns = new Vector4[4];
            for (int i = 0; i < 4; i++)
            {
                context.Reader.ReadStartArray();

                columns[i] = new Vector4(
                    (float)context.Reader.ReadDouble(),
                    (float)context.Reader.ReadDouble(),
                    (float)context.Reader.ReadDouble(),
                    (float)context.Reader.ReadDouble()
                );

                context.Reader.ReadEndArray();
            }

            context.Reader.ReadEndArray();

            return new Matrix4x4(
                columns[0],
                columns[1],
                columns[2],
                columns[3]
            );
        }

        public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, Matrix4x4 matrix)
        {
            // Start container array
            context.Writer.WriteStartArray();

            // Fill with arrays representing columns
            for (int i = 0; i < 4; i++)
            {
                context.Writer.WriteStartArray();

                Vector4 columnVector = matrix.GetColumn(i);
                context.Writer.WriteDouble(columnVector.x);
                context.Writer.WriteDouble(columnVector.y);
                context.Writer.WriteDouble(columnVector.z);
                context.Writer.WriteDouble(columnVector.w);

                context.Writer.WriteEndArray();
            }

            // Close contrainer array
            context.Writer.WriteEndArray();
        }

        public bool TryGetMemberSerializationInfo(string memberName, out BsonSerializationInfo serializationInfo)
        {
            serializationInfo = null;
            return true;
        }
    }
}
