using System;
using System.Collections.Generic;
using OctoDB.Diagnostics;

namespace OctoDB.Storage
{
    public class Store
    {
        readonly IStatistics statistics;
        readonly IStorageEngine storageEngine;
        readonly DocumentEncoder documentEncoder;
        readonly object readSnapshotLock = new object();
        DocumentSet lastReadSnapshot;

        public Store(string rootPath)
        {
            statistics = new Statistics();
            storageEngine = new StorageEngine(rootPath, statistics);
            documentEncoder = new DocumentEncoder(statistics);
        }

        public IStorageEngine StorageEngine { get { return storageEngine; } }

        public IStatistics Statistics { get { return statistics; } }

        public ReadOnlySession OpenReadSession()
        {
            statistics.IncrementReadSessionsOpened();

            var anchor = storageEngine.GetCurrentAnchor();
            
            lock (readSnapshotLock)
            {
                if (lastReadSnapshot != null && lastReadSnapshot.Anchor.Id == anchor.Id)
                {
                    statistics.IncrementSnapshotReuse();
                    return new ReadOnlySession(anchor, lastReadSnapshot, DisposeReadSession);
                }

                statistics.IncrementSnapshotRebuild();

                var documents = new DocumentSet(documentEncoder, anchor);
                if (lastReadSnapshot != null)
                {
                    documents.InitializeFrom(lastReadSnapshot);
                }

                var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var visitor = new LoadEverythingVisitor(documents, visited);
                storageEngine.Visit(anchor, visitor);
                lastReadSnapshot = documents;

                if (lastReadSnapshot != null)
                {
                    documents.RemoveExcept(visited);
                }

                return new ReadOnlySession(anchor, documents, DisposeReadSession);
            }
        }

        public WriteableSession OpenWriteSession()
        {
            statistics.IncrementWriteSessionsOpened();
            var anchor = storageEngine.GetCurrentAnchor();
            return new WriteableSession(storageEngine, statistics, documentEncoder, anchor, DisposeWriteSession);
        }

        void DisposeReadSession()
        {
            statistics.IncrementReadSessionsClosed();
        }

        void DisposeWriteSession()
        {
            statistics.IncrementWriteSessionsClosed();
        }
    }
}