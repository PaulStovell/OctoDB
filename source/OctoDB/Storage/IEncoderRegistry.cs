using System.IO;

namespace OctoDB.Storage
{
    public interface IEncoderRegistry
    {
        object Decode(string path, Stream input);
        void Encode(string path, object document, Stream output);
    }
}