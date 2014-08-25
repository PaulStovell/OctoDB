using System;
using System.IO;
using System.Runtime.Serialization;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using OctoDB.Diagnostics;
using OctoDB.Util;

namespace OctoDB.Storage
{
    public class DocumentEncoder : IDocumentEncoder
    {
        readonly JsonSerializerSettings serializerSettings;
        readonly IStatistics statistics;

        public DocumentEncoder(IStatistics statistics)
        {
            this.statistics = statistics;
            serializerSettings = new JsonSerializerSettings();
            serializerSettings.Converters.Add(new StringEnumConverter());
            serializerSettings.Formatting = Formatting.Indented;
            serializerSettings.ContractResolver = new OctoDbContractResolver();
        }

        public JsonSerializerSettings SerializerSettings
        {
            get { return serializerSettings; }
        }

        public object Read(Stream stream, Type type, ProvideAttachmentStreamCallback provideAttachment)
        {
            using (statistics.MeasureDeserialization())
            using (var streamReader = new StreamReader(stream, Encoding.UTF8))
            using (var jsonReader = new JsonTextReader(streamReader))
            {
                var serializer = CreateSerializer(provideAttachment);
                var result = serializer.Deserialize(jsonReader, type);
                if (result != null)
                {
                    statistics.IncrementLoaded();
                }
                return result;
            }
        }

        public void Write(Stream stream, object document, Type type, ProvideAttachmentStreamCallback provideAttachment)
        {
            using (statistics.MeasureSerialization())
            {
                using (var streamWriter = new StreamWriter(stream, Encoding.UTF8))
                {
                    var writer = new JsonTextWriter(streamWriter);
                    var serializer = CreateSerializer(provideAttachment);
                    serializer.Serialize(writer, document, typeof (object));
                    streamWriter.Flush();
                }
            }
        }

        JsonSerializer CreateSerializer(ProvideAttachmentStreamCallback provideAttachment)
        {
            var serializer = JsonSerializer.Create(serializerSettings);
            serializer.Context = new StreamingContext(serializer.Context.State, new OctoDbSerializationContext(
                onRequestAttachment: delegate(object[] owners, object owner, string propertyName, AttachedAttribute attribute)
                {
                    using (statistics.MeasureAttachments())
                    {
                        var key = GetAttachmentKey(owners, attribute);
                        object result = null;
                        provideAttachment(key, stream =>
                        {
                            result = ReadAttachment(stream, attribute);
                        });
                        return result;
                    }
                },
                onNotifyAttachment: delegate(object[] owners, object owner, string propertyName, object value, AttachedAttribute attribute)
                {
                    using (statistics.MeasureAttachments())
                    {
                        var key = GetAttachmentKey(owners, attribute);
                        provideAttachment(key, stream => WriteAttachment(stream, value, attribute));
                    }
                }
                ));
            return serializer;
        }

        static void WriteAttachment(Stream stream, object value, AttachedAttribute attribute)
        {
            using (var writer = new StreamWriter(stream, Encoding.UTF8))
            {
                writer.Write(value);
                writer.Flush();
            }
        }

        static object ReadAttachment(Stream stream, AttachedAttribute attribute)
        {
            var reader = new StreamReader(stream, Encoding.UTF8);
            return reader.ReadToEnd();
        }

        static string GetAttachmentKey(object[] owner, AttachedAttribute attribute)
        {
            var owners = new object[owner.Length];
            var j = 0;
            for (var i = owner.Length - 1; i >= 0; i--, j++)
            {
                var source = owner[i];
                owners[j] = PropertyReaderFactory.Create<object>(source.GetType(), "Id").Read(source);
            }

            var formatString = attribute.NameFormat;
            if (owners.Length > 0)
            {
                formatString = formatString.Replace("{id}", "{0}");
            }
            if (owners.Length > 1)
            {
                formatString = formatString.Replace("{parent}", "{1}");
            }

            for (var i = 2; i < owners.Length; i++)
            {
                formatString = formatString.Replace("{parent" + i + "}", "{" + i + "}");
            }

            return string.Format(formatString, owners);
        }
    }
}