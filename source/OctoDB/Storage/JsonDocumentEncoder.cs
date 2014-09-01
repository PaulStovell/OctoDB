using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using OctoDB.Diagnostics;

namespace OctoDB.Storage
{
    public class JsonDocumentEncoder : IEncoder
    {
        readonly JsonSerializerSettings serializerSettings;
        readonly IStatistics statistics;

        public JsonDocumentEncoder(IStatistics statistics)
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

        JsonSerializer CreateSerializer()
        {
            var serializer = JsonSerializer.Create(serializerSettings);
            return serializer;
        }

        public bool CanDecode(string path)
        {
            var type = Conventions.GetType(path);
            return type != null;
        }

        public object Decode(string path, Stream input)
        {
            var type = Conventions.GetType(path);
            using (statistics.MeasureDeserialization())
            using (var streamReader = new StreamReader(input, Encoding.UTF8))
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

        public void Encode(string type, object document, Stream output)
        {
            using (statistics.MeasureSerialization())
            {
                using (var streamWriter = new StreamWriter(output, Encoding.UTF8))
                {
                    var writer = new JsonTextWriter(streamWriter);
                    var serializer = CreateSerializer();
                    serializer.Serialize(writer, document, typeof(object));
                    streamWriter.Flush();
                }
            }
        }

        public bool CanEncode(Type type)
        {
            return Conventions.IsSupported(type);
        }
    }
}