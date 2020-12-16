namespace server.DBConnection.SerializationTools
{
    using MongoDB.Bson.IO;
    using MongoDB.Bson.Serialization;
    using OpenUp.DataStructures.ValueStructures;
    using System;
    using System.Collections.Generic;
    using Utils;

    class InterfaceSerializer : IBsonSerializer
        {
            private readonly Dictionary<string, IBsonSerializer> specifics = new Dictionary<string, IBsonSerializer>();
            private readonly IEnumerable<Type>                   implementers;

            public InterfaceSerializer(Type _interface, IEnumerable<Type> implementers)
            {
                ValueType         = _interface;
                
                this.implementers = implementers;
            }

            public void Initialize()
            {
                foreach (Type implementer in implementers)
                {
                    IBsonSerializer impl = BsonSerializer.SerializerRegistry.GetSerializer(implementer);
                    specifics.Add(implementer.Name, impl);
                }
            }

            public object Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
            {
                context.Reader.ReadStartDocument();

                string             typeName = context.Reader.ReadString("_t");
                BsonReaderBookmark mark     = context.Reader.GetBookmark();

                object value    = null;
                bool   hasValue = context.Reader.FindElement("_value");

                if (hasValue)
                {
                    IBsonSerializer specific = specifics[typeName];
                    value = specific.Deserialize(context);
                }
                else
                {
                    IThing specific = specifics[typeName] as IThing;
                    context.Reader.ReturnToBookmark(mark);
                    value = specific._Deserialize(context, mark);
                }

                context.Reader.LeaveDocument();

                return value;
            }

            public void Serialize(BsonSerializationContext context, BsonSerializationArgs args, object value)
            {
                string typeName = value.GetType().Name;

                context.Writer.WriteStartDocument();

                context.Writer.WriteName("_t");
                context.Writer.WriteString(typeName);

                context.Writer.WriteName("_value");
                specifics[typeName].Serialize(context, value);
                context.Writer.WriteEndDocument();
            }

            public Type ValueType { get; }
        }
}