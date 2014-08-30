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
        }

        public JsonSerializerSettings SerializerSettings
        {
            get { return serializerSettings; }
        }

        public object Read(Stream stream, Type type)
        {
            using (statistics.MeasureDeserialization())
            using (var streamReader = new StreamReader(stream, Encoding.UTF8))
            using (var jsonReader = new JsonTextReader(streamReader))
            {
                var serializer = CreateSerializer();
                var result = serializer.Deserialize(jsonReader, type);
                if (result != null)
                {
                    statistics.IncrementLoaded();
                }
                return result;
            }
        }

        public void Write(Stream stream, object document, Type type)
        {
            using (statistics.MeasureSerialization())
            {
                using (var streamWriter = new StreamWriter(stream, Encoding.UTF8))
                {
                    var writer = new JsonTextWriter(streamWriter);
                    var serializer = CreateSerializer();
                    serializer.Serialize(writer, document, typeof (object));
                    streamWriter.Flush();
                }
            }
        }

        JsonSerializer CreateSerializer()
        {
            var serializer = JsonSerializer.Create(serializerSettings);
            return serializer;
        }
    }
}