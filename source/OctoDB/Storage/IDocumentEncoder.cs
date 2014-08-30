using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using OctoDB.Diagnostics;

namespace OctoDB.Storage
{
    public interface ICodec
    {
        object Decode(string path, Stream input);
        void Encode(string path, object document, Stream output);
    }

    public class EncoderSelector : Collection<IEncoder>, ICodec
    {
        public object Decode(string path, Stream input)
        {
            var encoder = this.First(e => e.CanDecode(path));
            return encoder.Decode(path, input);
        }

        public void Encode(string path, object document, Stream output)
        {
            var type = document.GetType();
            var encoder = this.First(e => e.CanEncode(type));
            encoder.Encode(path, document, output);
        }
    }

    public interface IEncoder
    {
        bool CanDecode(string path);
        object Decode(string path, Stream input);
        void Encode(string path, object document, Stream output);
        bool CanEncode(Type type);
    }

    public class BlobEncoder : IEncoder
    {
        public bool CanDecode(string path)
        {
            return true;
        }

        public object Decode(string path, Stream input)
        {
            using (var buffer = new MemoryStream())
            {
                input.CopyTo(buffer);
                return buffer.ToArray();                
            }
        }

        public void Encode(string path, object document, Stream output)
        {
            var data = (byte[]) document;
            using (var writer = new BinaryWriter(output))
            {
                writer.Write(data);
                writer.Flush();
            }
        }

        public bool CanEncode(Type type)
        {
            return type == typeof (byte[]);
        }
    }

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