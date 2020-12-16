namespace server.DBConnection
{
    using MongoDB.Bson.Serialization;
    using OpenUp.DataStructures.ValueStructures;
    using SerializationTools;

    public static class AppStorageMap
    {
        public static void InitMapping()
        {
            BsonSerializer.RegisterSerializer(new ColorSerializer());
            BsonSerializer.RegisterSerializer(new Matrix4x4Serializer());
            BsonSerializer.RegisterSerializer(new TransformSerializer());
            BsonSerializer.RegisterSerializer(new UpdateSerializer());
            BsonSerializer.RegisterSerializer(new Vector3Serializer());
            BsonSerializer.RegisterSerializationProvider(new CustomSerializationProvider());

            BsonClassMap.RegisterClassMap<OUEnum>(
                cm =>
                {
                    cm.AutoMap();
                }
            );

            BsonSerializer.RegisterSerializer(
                new BsonClassMapSerializer<OUEnum>(
                    BsonClassMap.LookupClassMap(typeof(OUEnum))
                )
            );
        }
    }
}
