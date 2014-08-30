using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using OctoDB.Diagnostics;

namespace OctoDB.Storage
{
    public class WriteSession : IWriteSession, IWriteAttachments
    {
        readonly IStorageEngine storage;
        readonly IStatistics statistics;
        readonly ICodec encoder;
        readonly IAnchor anchor;
        readonly List<IWriteSessionExtension> extensions;
        readonly Action disposed;
        readonly DocumentSet documents;
        readonly Dictionary<string, object> pendingStores = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        readonly HashSet<string> pendingDeletes = new HashSet<string>();
        readonly IStorageBatch batch;

        public WriteSession(IStorageEngine storage, IStatistics statistics, ICodec encoder, IAnchor anchor, List<IWriteSessionExtension> extensions, Action disposed)
        {
            this.storage = storage;
            this.statistics = statistics;
            this.encoder = encoder;
            this.anchor = anchor;
            this.extensions = extensions;
            this.disposed = disposed;
            documents = new DocumentSet(anchor);

            CallExtensions((ext, ctx) => ext.AfterOpen(ctx));

            batch = storage.Batch();
        }

        public IWriteAttachments Attachments { get { return this; } }

        public IAnchor Anchor { get { return anchor; } }

        public T Load<T>(string id) where T : class
        {
            var visitor = new LoadByIdVisitor<T>(new List<string> {id}, documents, encoder);
            storage.Visit(anchor, visitor);

            return visitor.Loaded.FirstOrDefault();
        }

        public List<T> Load<T>(string[] ids) where T : class
        {
            var visitor = new LoadByIdVisitor<T>(new List<string>(ids), documents, encoder);
            storage.Visit(anchor, visitor);
            return visitor.Loaded;
        }

        public List<T> Query<T>() where T : class
        {
            var visitor = new LoadByTypeVisitor<T>(documents, encoder);
            storage.Visit(anchor, visitor);
            return visitor.Loaded;
        }

        public void Store(object item)
        {
            if (item == null) 
                return;
            CallExtensions((ext, ctx) => ext.BeforeStore(item, ctx));

            var path = Conventions.GetPath(item);
            documents.Add(path, item, false);
            pendingStores[path] = item;

            CallExtensions((ext, ctx) => ext.AfterStore(item, ctx));
        }

        public void Delete(object item)
        {
            if (item == null)
                return;

            CallExtensions((ext, ctx) => ext.BeforeDelete(item, ctx));
            var path = Conventions.GetPath(item);
            pendingDeletes.Add(path);
            CallExtensions((ext, ctx) => ext.AfterDelete(item, ctx));
        }

        public void Commit(string message)
        {
            CallExtensions((ext, ctx) => ext.BeforeCommit(batch, ctx));

            foreach (var delete in pendingDeletes)
            {
                batch.Delete(delete);

                statistics.IncrementDeleted();
            }

            foreach (var store in pendingStores)
            {
                var document = store.Value;

                statistics.IncrementStored();

                var path = store.Key;
                batch.Put(path, stream => encoder.Encode(path, document, stream));
            }

            batch.Commit(message);

            CallExtensions((ext, ctx) => ext.AfterCommit(ctx));
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

        void IWriteAttachments.Store(string path, byte[] contents)
        {
            pendingStores[path] = contents;
            documents.Add(path, contents, true);
        }

        void IWriteAttachments.Store(string path, string contents, Encoding encoding)
        {
            encoding = encoding ?? Encoding.UTF8;
            var bytes = encoding.GetBytes(contents);
            ((IWriteAttachments)this).Store(path, bytes);
        }

        void IWriteAttachments.Delete(string path)
        {
            pendingDeletes.Add(path);
        }

        byte[] IReadAttachments.LoadBinary(string path)
        {
            var visitor = new LoadBlobVisitor(new List<string> { path }, documents, encoder);
            storage.Visit(anchor, visitor);

            if (visitor.Loaded.ContainsKey(path))
            {
                return visitor.Loaded[path];
            }
            return null;
        }

        string IReadAttachments.LoadText(string path, Encoding encoding)
        {
            encoding = encoding ?? Encoding.UTF8;
            var bytes = ((IWriteAttachments)this).LoadBinary(path);
            return encoding.GetString(bytes);
        }
    }

    public class HistoricalReadSession : IReadSession, IReadAttachments
    {
        readonly IStorageEngine storage;
        readonly ICodec encoder;
        readonly IAnchor anchor;
        readonly Action disposed;
        readonly DocumentSet documents;

        public HistoricalReadSession(IStorageEngine storage, ICodec encoder, IAnchor anchor, Action disposed)
        {
            this.storage = storage;
            this.encoder = encoder;
            this.anchor = anchor;
            this.disposed = disposed;
            documents = new DocumentSet(anchor);
        }

        public IReadAttachments Attachments { get { return this; } }

        public IAnchor Anchor { get { return anchor; } }

        public T Load<T>(string id) where T : class
        {
            var visitor = new LoadByIdVisitor<T>(new List<string> { id }, documents, encoder);
            storage.Visit(anchor, visitor);

            return visitor.Loaded.FirstOrDefault();
        }

        public List<T> Load<T>(string[] ids) where T : class
        {
            var visitor = new LoadByIdVisitor<T>(new List<string>(ids), documents, encoder);
            storage.Visit(anchor, visitor);
            return visitor.Loaded;
        }

        public ReadOnlyCollection<T> Query<T>() where T : class
        {
            var visitor = new LoadByTypeVisitor<T>(documents, encoder);
            storage.Visit(anchor, visitor);
            return new ReadOnlyCollection<T>(visitor.Loaded);
        }

        public void Dispose()
        {
            if (disposed != null) disposed();
        }

        byte[] IReadAttachments.LoadBinary(string path)
        {
            var visitor = new LoadBlobVisitor(new List<string> { path }, documents, encoder);
            storage.Visit(anchor, visitor);

            if (visitor.Loaded.ContainsKey(path))
            {
                return visitor.Loaded[path];
            }
            return null;
        }

        string IReadAttachments.LoadText(string path, Encoding encoding)
        {
            encoding = encoding ?? Encoding.UTF8;
            var bytes = ((IReadAttachments)this).LoadBinary(path);
            return encoding.GetString(bytes);
        }
    }
}