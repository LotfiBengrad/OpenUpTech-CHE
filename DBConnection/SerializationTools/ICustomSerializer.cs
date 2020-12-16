namespace server.DBConnection.SerializationTools
{
    using MongoDB.Bson.IO;
    using MongoDB.Bson.Serialization;
    using System;

    interface IThing : IBsonSerializer
    {
        Func<BsonDeserializationContext, BsonReaderBookmark, object> _Deserialize { get; }
        Action<BsonSerializationContext, object>                     _Serialize   { get; }

        void Initialize();
    }
}