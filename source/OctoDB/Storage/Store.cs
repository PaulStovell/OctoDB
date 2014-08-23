using OctoDB.Diagnostics;

namespace OctoDB.Storage
{
    public class Store
    {
        readonly IStorageEngine storageEngine;
        readonly object sync = new object();
        IAnchor latestAnchor;

        public Store(string rootPath)
        {
            storageEngine = new StorageEngine(rootPath);
        }

        public Store(IStorageEngine engine)
        {
            storageEngine = engine;
        }

        public ReadOnlySession OpenReadSession()
        {
            storageEngine.Statistics.IncrementReadSessionsOpened();
            var anchor = storageEngine.GetCurrentAnchor();
            lock (sync)
            {
                var documents = storageEngine.LoadSnapshot(anchor, latestAnchor);
                latestAnchor = anchor;
                return new ReadOnlySession(storageEngine, latestAnchor, documents);
            }
        }

        public WriteableSession OpenWriteSession()
        {
            storageEngine.Statistics.IncrementWriteSessionsOpened();
            var anchor = storageEngine.GetCurrentAnchor();
            return new WriteableSession(storageEngine, anchor);
        }
    }
}