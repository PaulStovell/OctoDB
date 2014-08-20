using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Remoting.Messaging;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using OctoDB.Util;

namespace OctoDB.Storage
{
    public class GitDbContractResolver : CamelCasePropertyNamesContractResolver
    {
        protected override JsonContract CreateContract(Type objectType)
        {
            var interceptors = new List<AttachmentInterceptor>();
            CallContext.SetData("GitDbAttachmentInterceptors", interceptors);

            var serializer = base.CreateContract(objectType);

            if (interceptors.Count > 0)
            {
                serializer.OnDeserializedCallbacks.Add((o, a) =>
                {
                    var context = (GitDbSerializationContext)a.Context;
                    foreach (var interceptor in interceptors)
                    {
                        interceptor.WriteAttachment(o, context);
                    }
                });

                serializer.OnSerializedCallbacks.Add((o, a) =>
                {
                    var context = (GitDbSerializationContext)a.Context;
                    foreach (var interceptor in interceptors)
                    {
                        var found = interceptor.ReadAttachment(o);
                        if (found != null)
                        {
                            context.Attachments.Add(found);
                        }
                    }
                });
            }

            CallContext.FreeNamedDataSlot("GitDbAttachmentInterceptors");

            return serializer;
        }

        protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
        {
            var property = base.CreateProperty(member, memberSerialization);
            var attribute = (ExternalAttribute)member.GetCustomAttributes(typeof(ExternalAttribute), true).FirstOrDefault();
            if (attribute != null)
            {
                var interceptors = (List<AttachmentInterceptor>)CallContext.GetData("GitDbAttachmentInterceptors");
                interceptors.Add(new AttachmentInterceptor(member, property.PropertyName, attribute));

                property.Ignored = true;
            }

            return property;
        }

        class AttachmentInterceptor
        {
            private readonly string propertyName;
            private readonly ExternalAttribute attribute;
            private readonly IPropertyReaderWriter<object> readerWriter;

            public AttachmentInterceptor(MemberInfo member, string propertyName, ExternalAttribute attribute)
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

            public void WriteAttachment(object instance, GitDbSerializationContext context)
            {
                var value = context.RequestAttachment(instance, propertyName, attribute);
                readerWriter.Write(instance, value);
            }
        }
    }
}