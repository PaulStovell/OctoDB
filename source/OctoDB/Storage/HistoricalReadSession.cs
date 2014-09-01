using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace OctoDB.Storage
{
    public class HistoricalReadSession : IReadSession, IReadAttachments
    {
        readonly IStorageEngine storage;
        readonly IEncoderRegistry encoder;
        readonly IAnchor anchor;
        readonly Action disposed;
        readonly DocumentSet documents;

        public HistoricalReadSession(IStorageEngine storage, IEncoderRegistry encoder, IAnchor anchor, Action disposed)
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