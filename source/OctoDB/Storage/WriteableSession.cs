using System.Collections.Generic;
using OctoDB.Diagnostics;

namespace OctoDB.Storage
{
    public class WriteableSession : IWriteableSession
    {
        readonly IStorageEngine storage;
        readonly IAnchor anchor;
        readonly HashSet<object> pendingStores = new HashSet<object>();
        readonly HashSet<object> pendingDeletes = new HashSet<object>(); 

        public WriteableSession(IStorageEngine storage, IAnchor anchor)
        {
            this.storage = storage;
            this.anchor = anchor;
        }

        public T Load<T>(string id) where T : class
        {
            return storage.Load<T>(anchor, id);
        }

        public List<T> Query<T>() where T : class
        {
            return storage.LoadAll<T>(anchor);
        }

        public void Store(object item)
        {
            pendingStores.Add(item);
        }

        public void Delete(object item)
        {
            pendingDeletes.Add(item);
        }

        public void Commit(string message)
        {
            using (var batch = storage.Batch())
            {
                foreach (var delete in pendingDeletes)
                {
                    batch.Delete(delete);
                }

                foreach (var store in pendingStores)
                {
                    batch.Put(store);                    
                }

                batch.Commit(message);
            }
        }

        public void Dispose()
        {
            storage.Statistics.IncrementWriteSessionsClosed();
        }
    }
}