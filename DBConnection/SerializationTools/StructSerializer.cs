namespace server.DBConnection.SerializationTools
{
    using MongoDB.Bson.IO;
    using MongoDB.Bson.Serialization;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;
    using Utils;

    class StructSerializer : IThing
    {
        public Func<BsonDeserializationContext, BsonReaderBookmark, object> _Deserialize { get; private set; }
        public Action<BsonSerializationContext, object> _Serialize { get; private set; }

        public StructSerializer(Type _struct)
        {
            ValueType = _struct;
            
            Console.WriteLine($"Creating deserializer for: {ValueType}");
        }

        public void Initialize()
        {
            CreateSerialize();
            CreateDeserialize();
        }

        private void CreateSerialize()
        {
            ParameterExpression obj  = Expression.Parameter(typeof(object),                   "obj");
            ParameterExpression data = Expression.Parameter(typeof(BsonSerializationContext), "data");
            
            UnaryExpression cast = Expression.Convert(obj, ValueType);

            List<Expression> assignments = new List<Expression>
            {
                cast
            };

            foreach (FieldInfo field in ValueType.GetFields())
            {
                IBsonSerializer serializer = BsonSerializer.SerializerRegistry.GetSerializer(field.FieldType);

                Expression<Action<BsonSerializationContext>> name = cont => cont.Writer.WriteName(field.Name);
                Expression<Action<BsonSerializationContext, object>> setter = (cont, o) => serializer.Serialize(cont, o);
                
                InvocationExpression invReader = Expression.Invoke(name, data);
                MemberExpression     getter    = Expression.PropertyOrField(cast, field.Name);
                UnaryExpression      castGet   = Expression.Convert(getter, typeof(object));
                InvocationExpression invSetter = Expression.Invoke(setter, data, castGet);

                assignments.Add(invReader);
                assignments.Add(invSetter);
            }
            
            foreach (PropertyInfo property in ValueType.GetProperties())
            {
                IBsonSerializer serializer = BsonSerializer.SerializerRegistry.GetSerializer(property.PropertyType);

                if (property.GetIndexParameters().Length > 0)
                {
                    throw new NotSupportedException("Indexers on custom serializations is not supported");
                }
                else
                {
                    Expression<Action<BsonSerializationContext>> namer = cont => cont.Writer.WriteName(property.Name);
                    Expression<Action<BsonSerializationContext, object>> setter = (cont, o) => serializer.Serialize(cont, o);
                    
                    InvocationExpression invNamer  = Expression.Invoke(namer, data);
                    MemberExpression     getter    = Expression.PropertyOrField(cast, property.Name);
                    UnaryExpression      castGet   = Expression.Convert(getter, typeof(object));
                    ParameterExpression  gotten    = Expression.Variable(typeof(object));
                    BinaryExpression assignGotten  = Expression.Assign(gotten, castGet);
                    InvocationExpression invSetter = Expression.Invoke(setter, data, gotten);
                    
                    assignments.Add(
                        Expression.TryCatch(
                            Expression.Block(
                                assignGotten,
                                invNamer, 
                                invSetter
                            ),
                            new CatchBlock[]
                            {
                                Expression.Catch(
                                    Expression.Variable(typeof(Exception)), 
                                    (Expression<Action<Exception>>)(e => Console.WriteLine($"Expression caught an exception: {e.Message}"))
                                )
                            }
                        )
                    );
                    
                    // assignments.Add(invNamer);
                    // assignments.Add(invSetter);
                }
            }
            
            BlockExpression block = Expression.Block(
                assignments
            );
            
            _Serialize = Expression.Lambda<Action<BsonSerializationContext, object>>(block, data, obj)
                                     .Compile();
        }
        
        private void CreateDeserialize()
        {
            ParameterExpression obj = Expression.Parameter(ValueType, "obj");
            ParameterExpression isThere = Expression.Parameter(typeof(bool), "isThere");
            ParameterExpression contextParam = Expression.Parameter(typeof(BsonDeserializationContext), "context");
            ParameterExpression bookmarkParam = Expression.Parameter(typeof(BsonReaderBookmark), "bookmark");
            
            NewExpression create = Expression.New(ValueType);
            List<Expression> assignments = new List<Expression>
            {
                Expression.Assign(obj, create)
            };

            foreach (FieldInfo field in ValueType.GetFields())
            {
                IBsonSerializer serializer = BsonSerializer.SerializerRegistry.GetSerializer(field.FieldType);
                
                Expression<Func<BsonDeserializationContext, bool>> reader = cont => cont.Reader.FindElement(field.Name);
                Expression<Func<BsonDeserializationContext, object>> getter = cont => serializer.Deserialize(cont);
                Expression<Action<BsonDeserializationContext, BsonReaderBookmark>> reset = (cont, bkmrk) => cont.Reader.ReturnToBookmark(bkmrk);
                
                InvocationExpression invReader = Expression.Invoke(reader, contextParam);
                InvocationExpression invGetter = Expression.Invoke(getter, contextParam);
                InvocationExpression invReset = Expression.Invoke(reset, contextParam, bookmarkParam);

                UnaryExpression cast = Expression.Convert(invGetter, field.FieldType);
                MemberExpression f = Expression.PropertyOrField(obj, field.Name);

                BinaryExpression assign = Expression.Assign(f, cast);
                
                ConditionalExpression cond = Expression.IfThen(invReader, assign);
                
                assignments.Add(cond);
                assignments.Add(invReset);
            }
            
            foreach (PropertyInfo property in ValueType.GetProperties())
            {
                IBsonSerializer serializer = BsonSerializer.SerializerRegistry.GetSerializer(property.PropertyType);
                
                if (property.GetIndexParameters().Length > 0)
                {
                    throw new NotSupportedException("Indexers on custom serializations is not supported");
                }
                else
                {
                    Expression<Func<BsonDeserializationContext, bool>> reader = cont => cont.Reader.FindElement(property.Name);
                    Expression<Func<BsonDeserializationContext, object>> getter = cont => serializer.Deserialize(cont);
                    Expression<Action<BsonDeserializationContext, BsonReaderBookmark>> reset = (cont, bkmrk) => cont.Reader.ReturnToBookmark(bkmrk);

                    InvocationExpression invReader = Expression.Invoke(reader, contextParam);
                    InvocationExpression invGetter = Expression.Invoke(getter, contextParam);
                    InvocationExpression invReset  = Expression.Invoke(reset, contextParam, bookmarkParam);

                    UnaryExpression cast = Expression.Convert(invGetter, property.PropertyType);
                    MemberExpression f = Expression.PropertyOrField(obj, property.Name);

                    BinaryExpression assign = Expression.Assign(f, cast);
                    ConditionalExpression cond = Expression.IfThen(invReader, assign);

                    assignments.Add(cond);
                    // assignments.Add(assign);
                    assignments.Add(invReset);
                }
            }
            
            assignments.Add(Expression.Convert(obj, typeof(object)));
            
            
            BlockExpression block = Expression.Block(
                new[] {obj}, 
                assignments
            );
            
            _Deserialize = Expression.Lambda<Func<BsonDeserializationContext, BsonReaderBookmark, object>>(block, contextParam, bookmarkParam)
                                     .Compile();
        }
        
        
        public object Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
        {
            context.Reader.ReadStartDocument();
            BsonReaderBookmark mark = context.Reader.GetBookmark();
            
            object value = _Deserialize(context,  mark);
            
            context.Reader.LeaveDocument();

            return value;
        }

        public void Serialize(BsonSerializationContext context, BsonSerializationArgs args, object value)
        {
            context.Writer.WriteStartDocument();
            
            _Serialize(context, value);

            context.Writer.WriteEndDocument();
        }

        public Type ValueType { get; }
    }
}
