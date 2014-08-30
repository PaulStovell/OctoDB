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
        readonly List<IWriteSessionExtension> extensions;
        readonly Action disposed;
        readonly DocumentSet documents;
        readonly HashSet<object> pendingStores = new HashSet<object>();
        readonly HashSet<object> pendingDeletes = new HashSet<object>();
        readonly IStorageBatch batch;

        public WriteableSession(IStorageEngine storage, IStatistics statistics, IDocumentEncoder encoder, IAnchor anchor, List<IWriteSessionExtension> extensions, Action disposed)
        {
            this.storage = storage;
            this.statistics = statistics;
            this.encoder = encoder;
            this.anchor = anchor;
            this.extensions = extensions;
            this.disposed = disposed;
            documents = new DocumentSet(encoder, anchor);

            CallExtensions((ext, ctx) => ext.AfterOpen(ctx));

            batch = storage.Batch();
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
            CallExtensions((ext, ctx) => ext.BeforeStore(item, ctx));

            documents.Add(item);
            pendingStores.Add(item);

            CallExtensions((ext, ctx) => ext.AfterStore(item, ctx));
        }

        public void Delete(object item)
        {
            pendingDeletes.Add(item);
        }

        public void Commit(string message)
        {
            CallExtensions((ext, ctx) => ext.BeforeCommit(batch, ctx));

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

        void CallExtensions(Action<IWriteSessionExtension, ExtensionContext> callback)
        {
            var context = new ExtensionContext(this, anchor, storage);
            foreach (var extension in extensions)
            {
                callback(extension, context);
            }
        }

        public void Dispose()
        {
            batch.Dispose();
            if (disposed != null) disposed();
        }
    }
}