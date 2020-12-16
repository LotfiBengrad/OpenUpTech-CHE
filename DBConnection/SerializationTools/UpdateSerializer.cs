namespace server.DBConnection.SerializationTools
{
    using MongoDB.Bson;
    using MongoDB.Bson.IO;
    using MongoDB.Bson.Serialization;
    using MongoDB.Bson.Serialization.Serializers;
    using OpenUp.DataStructures;
    using OpenUp.Updating;
    using OpenUp.Utils;
    using System;
    using System.IO;
    using Utils;

    /// <summary>
    /// This thing needs a custom serializer to handle the fact that is has fields that error if accessed
    /// when not applicable.
    /// </summary>
    public class UpdateSerializer : SerializerBase<Update>, IBsonDocumentSerializer
    {
        public override Update Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
        {
            context.Reader.ReadStartDocument();

            int data = context.Reader.ReadInt32("type");
            Update update = new Update
            {
                type = (UpdateType)data
            };

            if (update.type != UpdateType.COMPOSITE)
            {
                IBsonSerializer pathSerializer = BsonSerializer.SerializerRegistry.GetSerializer(typeof(UpdatePath));
                context.Reader.ReadName("path");
                update.path = pathSerializer.Deserialize(context) as UpdatePath;

                if (update.type == UpdateType.SET)
                {
                    IBsonSerializer valueSerializer = BsonSerializer.SerializerRegistry.GetSerializer(typeof(IUpdateTarget));
                    
                    context.Reader.ReadName("value");
                
                    update.valueObject = valueSerializer.Deserialize(context) as IUpdateTarget;
                }
            }
            else
            {
                context.Reader.ReadName("updates");
                context.Reader.ReadStartArray();
                
                Update.UpdateList list = new Update.UpdateList();
                
                while (context.Reader.ReadBsonType() != BsonType.EndOfDocument)
                {
                    list.Add(this.Deserialize(context));
                }
                
                context.Reader.ReadEndArray();

                update.updates = list;
            }

            context.Reader.LeaveDocument();

            return update;
        }

        public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, Update update)
        {
            context.Writer.WriteStartDocument();
            
            context.Writer.WriteInt32("type", (int)update.type);

            if (update.type != UpdateType.COMPOSITE)
            {
                IBsonSerializer pathSerializer = BsonSerializer.SerializerRegistry.GetSerializer(typeof(UpdatePath));
                context.Writer.WriteName("path");
                pathSerializer.Serialize(context, args, update.path);

                if (update.type == UpdateType.SET)
                {
                    IBsonSerializer valueSerializer = BsonSerializer.SerializerRegistry.GetSerializer(typeof(IUpdateTarget));
                    context.Writer.WriteName("value");
                    valueSerializer.Serialize(context, args, update.valueObject);
                }
            }
            else
            {
                context.Writer.WriteName("updates");
                context.Writer.WriteStartArray();
                
                foreach (Update part in update.updates)
                {
                    this.Serialize(context, args, part);
                }

                context.Writer.WriteEndArray();
            }
                
            context.Writer.WriteEndDocument();
        }

        public bool TryGetMemberSerializationInfo(string memberName, out BsonSerializationInfo serializationInfo)
        {
            serializationInfo = null;
            
            switch (memberName)
            {
                case "type":
                {
                    serializationInfo = new BsonSerializationInfo(
                        memberName,
                        BsonSerializer.SerializerRegistry.GetSerializer(typeof(int)),
                        typeof(int)
                    );
                    break;
                }
                case "path":
                {
                    serializationInfo = new BsonSerializationInfo(
                        memberName,
                        BsonSerializer.SerializerRegistry.GetSerializer(typeof(UpdatePath)),
                        typeof(UpdatePath)
                    );
                    break;
                }
                case "value":
                {
                    serializationInfo = new BsonSerializationInfo(
                        memberName,
                        BsonSerializer.SerializerRegistry.GetSerializer(typeof(IUpdateTarget)),
                        typeof(IUpdateTarget)
                    );
                    break;
                }
                case "updates":
                {
                    serializationInfo = new BsonSerializationInfo(
                        memberName,
                        BsonSerializer.SerializerRegistry.GetSerializer(typeof(Update.UpdateList)),
                        typeof(Update.UpdateList)
                    );
                    break;
                }
            }
            
            return serializationInfo == null;
        }
    }
}