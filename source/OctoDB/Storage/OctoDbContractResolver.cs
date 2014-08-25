using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Remoting.Messaging;
using System.Security.Cryptography;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using OctoDB.Util;

namespace OctoDB.Storage
{
    public class OctoDbContractResolver : CamelCasePropertyNamesContractResolver
    {
        [ThreadStatic]
        static Stack<object> serializeStack;
        [ThreadStatic]
        static Stack<object> deserializeStack;

        public Stack<object> SerializeStack
        {
            get { return serializeStack = (serializeStack ?? new Stack<object>()); }
        }

        public Stack<object> DeserializeStack
        {
            get { return deserializeStack = (deserializeStack ?? new Stack<object>()); }
        } 

        protected override JsonContract CreateContract(Type objectType)
        {
            var interceptors = new List<AttachmentInterceptor>();
            CallContext.SetData("OctoDbAttachmentInterceptors", interceptors);

            var serializer = base.CreateContract(objectType);

            if (interceptors.Count > 0)
            {
                serializer.OnDeserializingCallbacks.Add((o, a) => DeserializeStack.Push(o));

                serializer.OnDeserializedCallbacks.Add((o, a) =>
                {
                    var context = (OctoDbSerializationContext)a.Context;
                    foreach (var interceptor in interceptors)
                    {
                        interceptor.WriteAttachment(DeserializeStack.ToArray(), o, context);
                    }
                    DeserializeStack.Pop();
                });

                serializer.OnSerializingCallbacks.Add((o, a) => SerializeStack.Push(o));

                serializer.OnSerializedCallbacks.Add((o, a) =>
                {
                    var context = (OctoDbSerializationContext)a.Context;
                    foreach (var interceptor in interceptors)
                    {
                        var found = interceptor.ReadAttachment(o);
                        if (found != null)
                        {
                            context.NotifyAttachment(SerializeStack.ToArray(), found.Owner, found.PropertyName, found.Value, found.Attribute);
                        }
                    }
                    SerializeStack.Pop();
                });
            }

            CallContext.FreeNamedDataSlot("OctoDbAttachmentInterceptors");

            return serializer;
        }

        protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
        {
            var property = base.CreateProperty(member, memberSerialization);
            var attribute = (AttachedAttribute)member.GetCustomAttributes(typeof(AttachedAttribute), true).FirstOrDefault();
            if (attribute != null)
            {
                var interceptors = (List<AttachmentInterceptor>)CallContext.GetData("OctoDbAttachmentInterceptors");
                interceptors.Add(new AttachmentInterceptor(member, property.PropertyName, attribute));

                property.Ignored = true;
            }

            return property;
        }

        class AttachmentInterceptor
        {
            private readonly string propertyName;
            private readonly AttachedAttribute attribute;
            private readonly IPropertyReaderWriter<object> readerWriter;

            public AttachmentInterceptor(MemberInfo member, string propertyName, AttachedAttribute attribute)
            {
                this.propertyName = propertyName;
                this.attribute = attribute;

                readerWriter = PropertyReaderFactory.Create<object>(member.DeclaringType, member.Name);
            }

            public AttachmentFoundEvent ReadAttachment(object instance)
            {
                return new AttachmentFoundEvent
                {
                    Attribute = attribute,
                    Owner = instance,
                    PropertyName = propertyName,
                    Value = readerWriter.Read(instance)
                };
            }

            public void WriteAttachment(object[] deserializationStack, object instance, OctoDbSerializationContext context)
            {
                var value = context.RequestAttachmentValue(deserializationStack, instance, propertyName, attribute);
                readerWriter.Write(instance, value);
            }
        }
    }
}