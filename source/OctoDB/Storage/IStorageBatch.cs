using System;
using System.IO;

namespace OctoDB.Storage
{
    public interface IStorageBatch : IDisposable
    {
        void Put(string path, Action<Stream> streamWriter);
        void Delete(string path);
        void Commit(string message);
    }
}