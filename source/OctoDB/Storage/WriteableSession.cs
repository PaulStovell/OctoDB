using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using OctoDB.Diagnostics;

namespace OctoDB.Storage
{
    public interface IReadAttachments
    {
        byte[] Load(string path);
    }

    public static class AttachmentExtensions
    {
        public static string LoadText(this IReadAttachments attachments, string path, Encoding encoding = null)
        {
            encoding = encoding ?? Encoding.UTF8;
            var bytes = attachments.Load(path);
            return encoding.GetString(bytes);
        }
    }

    public interface IWriteAttachments : IReadAttachments
    {
        void Store(string path, byte[] contents);
        void Store(string path, string contents, Encoding encoding = null);
        void Delete(string path);
    }

    public class WriteableSession : IWriteableSession, IWriteAttachments
    {
        readonly IStorageEngine storage;
        readonly IStatistics statistics;
        readonly IDocumentEncoder encoder;
        readonly IAnchor anchor;
        readonly List<IWriteSessionExtension> extensions;
        readonly Action disposed;
        readonly DocumentSet documents;
        readonly Dictionary<string, object> pendingStores = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        readonly HashSet<string> pendingDeletes = new HashSet<string>();
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

        public IWriteAttachments Attachments { get { return this; } }

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

            var path = Conventions.GetPath(item);
            documents.Add(item);
            pendingStores[path] = item;

            CallExtensions((ext, ctx) => ext.AfterStore(item, ctx));
        }

        public void Delete(object item)
        {
            if (item == null)
                return;

            var path = Conventions.GetPath(item);
            pendingDeletes.Add(path);
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

                if (document is byte[])
                {
                    batch.Put(store.Key, stream =>
                    {
                        using (var writer = new BinaryWriter(stream))
                        {
                            writer.Write((byte[]) document);
                            writer.Flush();
                        }
                    });
                }
                else
                {
                    batch.Put(store.Key, stream => encoder.Write(stream, document, document.GetType()));
                }
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

        void IWriteAttachments.Store(string path, byte[] contents)
        {
            pendingStores[path] = contents;
            documents.Add(path, contents);
        }

        void IWriteAttachments.Store(string path, string contents, Encoding encoding = null)
        {
            encoding = encoding ?? Encoding.UTF8;
            var bytes = encoding.GetBytes(contents);
            ((IWriteAttachments)this).Store(path, bytes);
        }

        void IWriteAttachments.Delete(string path)
        {
            pendingDeletes.Add(path);
        }

        byte[] IReadAttachments.Load(string path)
        {
            var visitor = new LoadBlobVisitor(new List<string> { path }, documents);
            storage.Visit(anchor, visitor);

            if (visitor.Loaded.ContainsKey(path))
            {
                return visitor.Loaded[path];
            }
            return null;
        }
    }
}