using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OctoDB.Diagnostics;

namespace OctoDB.Storage
{
    public class WriteableSession : IWriteableSession
    {
        readonly IStorageEngine storage;
        readonly IStatistics statistics;
        readonly IDocumentEncoder encoder;
        readonly IAnchor anchor;
        readonly Action disposed;
        readonly DocumentSet documents;
        readonly HashSet<object> pendingStores = new HashSet<object>();
        readonly HashSet<object> pendingDeletes = new HashSet<object>();

        public WriteableSession(IStorageEngine storage, IStatistics statistics, IDocumentEncoder encoder, IAnchor anchor, Action disposed)
        {
            this.storage = storage;
            this.statistics = statistics;
            this.encoder = encoder;
            this.anchor = anchor;
            this.disposed = disposed;
            documents = new DocumentSet(encoder, anchor);
        }

        public T Load<T>(string id) where T : class
        {
            var visitor = new LoadByIdVisitor<T>(new List<string> {id}, documents);
            storage.Visit(anchor, visitor);

            return visitor.Loaded.FirstOrDefault();
        }

        public List<T> Load<T>(string[] ids) where T : class
        {
            var visitor = new LoadByIdVisitor<T>(new List<string>(ids), documents);
            storage.Visit(anchor, visitor);
            return visitor.Loaded;
        }

        public List<T> Query<T>() where T : class
        {
            var visitor = new LoadByTypeVisitor<T>(documents);
            storage.Visit(anchor, visitor);
            return visitor.Loaded;
        }

        public void Store(object item)
        {
            documents.Add(item);
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
                    var path = Conventions.GetPath(delete);
                    batch.Delete(path);

                    statistics.IncrementDeleted();
                }

                foreach (var store in pendingStores)
                {
                    var document = store;
                    var path = Conventions.GetPath(document);

                    statistics.IncrementStored();

                    batch.Put(path, stream => encoder.Write(stream, document, document.GetType(), (attachmentKey, attachmentWriter) =>
                    {
                        var attachmentPath = attachmentKey;
                        var parentDirectory = Path.GetDirectoryName(path);
                        if (parentDirectory != null)
                        {
                            attachmentPath = Path.Combine(parentDirectory, attachmentKey);
                        }

                        // ReSharper disable once AccessToDisposedClosure
                        batch.Put(attachmentPath, attachmentWriter);                     
                    }));
                }

                batch.Commit(message);
            }
        }

        public void Dispose()
        {
            if (disposed != null) disposed();
        }
    }
}