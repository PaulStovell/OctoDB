using System;
using System.Collections.Generic;
using OctoDB.Diagnostics;
using OctoDB.Storage;

namespace OctoDB
{
    public class Store : IDisposable
    {
        readonly IStatistics statistics;
        readonly IStorageEngine storageEngine;
        readonly object readSnapshotLock = new object();
        DocumentSet lastReadSnapshot;
        readonly EncoderSelector codec;

        public Store(string rootPath)
        {
            statistics = new Statistics();
            storageEngine = new StorageEngine(rootPath, statistics);

            codec = new EncoderSelector();
            codec.Add(new JsonDocumentEncoder(statistics));
            codec.Add(new BlobEncoder());
        }

        public IStorageEngine StorageEngine { get { return storageEngine; } }

        public IStatistics Statistics { get { return statistics; } }

        public IReadSession OpenReadSession()
        {
            var anchor = storageEngine.GetCurrentAnchor();
            statistics.IncrementReadSessionsOpened();

            lock (readSnapshotLock)
            {
                var documents = new DocumentSet(anchor);

                if (lastReadSnapshot != null && lastReadSnapshot.Anchor.Id == anchor.Id)
                {
                    statistics.IncrementSnapshotReuse();
                    return new ReadSession(anchor, lastReadSnapshot, DisposeReadSession);
                }

                statistics.IncrementSnapshotRebuild();
                if (lastReadSnapshot != null)
                {
                    documents.InitializeFrom(lastReadSnapshot);
                }

                var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var visitor = new LoadEverythingVisitor(documents, codec, visited);
                storageEngine.Visit(anchor, visitor);
                lastReadSnapshot = documents;

                if (lastReadSnapshot != null)
                {
                    documents.RemoveExcept(visited);
                }

                return new ReadSession(anchor, documents, DisposeReadSession);
            }
        }

        public IReadSession OpenReadSession(string commitSha)
        {
            var anchor = storageEngine.GetAnchor(commitSha);
            return OpenReadSession(anchor);
        }

        public IReadSession OpenReadSession(IAnchor anchor)
        {
            statistics.IncrementHistoricalReadSessionsOpened();

            return new HistoricalReadSession(storageEngine, codec, anchor, DisposeHistoricalReadSession);
        }

        public WriteSession OpenWriteSession()
        {
            statistics.IncrementWriteSessionsOpened();
            var anchor = storageEngine.GetCurrentAnchor();
            return new WriteSession(storageEngine, statistics, codec, anchor, new List<IWriteSessionExtension> { new LinearChunkIdentityGenerator() },  DisposeWriteSession);
        }

        void DisposeReadSession()
        {
            statistics.IncrementReadSessionsClosed();
        }

        void DisposeHistoricalReadSession()
        {
            statistics.IncrementHistoricalReadSessionsClosed();
        }

        void DisposeWriteSession()
        {
            statistics.IncrementWriteSessionsClosed();
        }

        public void Dispose()
        {
            storageEngine.Dispose();
        }

        public List<IAnchor> GetAnchors()
        {
            return storageEngine.GetAnchors();
        }
    }
}