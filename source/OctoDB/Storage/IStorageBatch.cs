using System;

namespace OctoDB.Storage
{
    public interface IStorageBatch : IDisposable
    {
        void Put(object o);
        void Delete(object o);
        void Commit(string message);
    }
}