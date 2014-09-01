using System;
using System.IO;

namespace OctoDB.Storage
{
    public interface IEncoder
    {
        bool CanDecode(string path);
        object Decode(string path, Stream input);
        void Encode(string path, object document, Stream output);
        bool CanEncode(Type type);
    }
}