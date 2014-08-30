using System;
using System.Collections.ObjectModel;

namespace OctoDB.Storage
{
    public interface IReadSession : IDisposable
    {
        IReadAttachments Attachments { get; }
        IAnchor Anchor { get; }
        T Load<T>(string id) where T : class;
        ReadOnlyCollection<T> Query<T>();
    }

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

        public ReadOnlyCollection<T> Query<T>()
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

        public byte[] Load(string path)
        {
            return (byte[])documents.Get(path);
        }
    }
}