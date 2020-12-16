namespace server.DBConnection.SerializationTools
{
    using MongoDB.Bson.Serialization;
    using OpenUp.DataStructures;
    using OpenUp.DataStructures.ValueStructures;
    using System;
    using System.Collections.Generic;
    using System.Linq;

    class CustomSerializationProvider : BsonSerializationProviderBase
        {
            private readonly Dictionary<Type, IBsonSerializer> cache = new Dictionary<Type, IBsonSerializer>();
            private readonly HashSet<Type>                     customTypes;

            private static HashSet<Type> targetTypes;

            public static bool HasCustomSerialization(Type type) => targetTypes.Contains(type);

            public CustomSerializationProvider()
            {
                customTypes = new HashSet<Type>(
                    AppDomain.CurrentDomain.GetAssemblies()
                             .SelectMany(asm => asm.GetTypes())
                             .Where(t => t.Namespace != null)
                             .Where(
                                 t => t.Namespace.StartsWith(nameof(OpenUp))
                                   || t.Namespace.StartsWith(nameof(server))
                             )
                );

                targetTypes = new HashSet<Type>(customTypes.Where(t => t.IsInterface));
                targetTypes.UnionWith(customTypes.Where(t => t.IsValueType && !t.IsEnum));
                targetTypes.Remove(typeof(TransformStructure));
            }

            public override IBsonSerializer GetSerializer(Type type, IBsonSerializerRegistry serializerRegistry)
            {
                if (!targetTypes.Contains(type)) return null;

                if (type.IsInterface)
                {
                    if (!cache.ContainsKey(type))
                    {
                        InterfaceSerializer interfaceSerializer = new InterfaceSerializer(
                            type,
                            customTypes.Where(t => type.IsAssignableFrom(t) && t != type)
                        );
                        cache[type] = interfaceSerializer;
                        interfaceSerializer.Initialize();
                    }

                    return cache[type];
                }
                else if (type.IsValueType && !type.IsEnum)
                {
                    if (!cache.ContainsKey(type))
                    {
                        Type   generic  = typeof(StructSerializer<OUValue>).GetGenericTypeDefinition();
                        Type   specific = generic.MakeGenericType(type);
                        IThing impl     = Activator.CreateInstance(specific, new object[] {type}) as IThing;

                        cache[type] = impl;

                        impl.Initialize();
                    }

                    return cache[type];
                }

                throw new InvalidOperationException($"Target type {type} has no serializer but is a target for custom serialization implementation");
            }
        }
}