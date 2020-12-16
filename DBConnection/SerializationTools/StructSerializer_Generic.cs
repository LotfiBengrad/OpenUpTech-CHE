namespace server.DBConnection.SerializationTools
{
    using MongoDB.Bson.IO;
    using MongoDB.Bson.Serialization;
    using System;

    class StructSerializer<T> : IBsonSerializer<T>, IThing
    {
        private StructSerializer serializer;
        private Type             type;

        public StructSerializer(Type type)
        {
            if (type != typeof(T)) throw new ArgumentException("Type is not generic param.");

            this.type = type;
        }

        public void Initialize()
        {
            serializer = new StructSerializer(type);
            serializer.Initialize();
        }

        object IBsonSerializer.Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
        {
            return serializer.Deserialize(context, args);
        }

        void IBsonSerializer.Serialize(BsonSerializationContext context, BsonSerializationArgs args, object value)
        {
            serializer.Serialize(context, args, value);
        }

        public void Serialize(BsonSerializationContext context, BsonSerializationArgs args, T value)
        {
            serializer.Serialize(context, args, value);
        }

        public T Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
        {
            return (T) serializer.Deserialize(context, args);
        }


        public Type                                                         ValueType    => serializer.ValueType;
        public Func<BsonDeserializationContext, BsonReaderBookmark, object> _Deserialize => serializer._Deserialize;

        public Action<BsonSerializationContext, object> _Serialize => serializer._Serialize;
    }
}