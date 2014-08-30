using System;
using System.Collections.ObjectModel;
using System.Text;

namespace OctoDB.Storage
{
    public class ReadSession : IReadSession, IReadAttachments
    {
        readonly IAnchor anchor;
        readonly DocumentSet documents;
        readonly Action disposed;

        public ReadSession(IAnchor anchor, DocumentSet documents, Action disposed)
        {
            this.anchor = anchor;
            this.documents = documents;
            this.disposed = disposed;
        }

        public IReadAttachments Attachments { get { return this; } }
        public IAnchor Anchor { get { return anchor; } }

        public T Load<T>(string id) where T : class
        {
            var path = Conventions.GetPath(typeof (T), id);
            return documents.Get(path) as T;
        }

        public ReadOnlyCollection<T> Query<T>() where T : class
        {
            return new ReadOnlyCollection<T>(documents.GetAll<T>());
        }

        public void Dispose()
        {
            if (disposed != null)
            {
                disposed();
            }
        }

        byte[] IReadAttachments.LoadBinary(string path)
        {
            return (byte[])documents.Get(path);
        }

        string IReadAttachments.LoadText(string path, Encoding encoding = null)
        {
            encoding = encoding ?? Encoding.UTF8;
            var bytes = ((IReadAttachments)this).LoadBinary(path);
            return encoding.GetString(bytes);
        }
    }
}