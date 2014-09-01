using System;
using System.IO;

namespace OctoDB.Storage
{
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
}