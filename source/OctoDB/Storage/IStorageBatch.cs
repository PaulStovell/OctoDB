using System;

namespace OctoDB.Storage
{
    public interface IStorageBatch : IDisposable
    {
        void Put(string uri, object o);
        void Delete(string uri);
        void Commit(string message);
    }
}