using System;
using System.Collections.Generic;
using OctoDB.Storage;

namespace OctoDB
{
    public interface IWriteSession : IDisposable
    {
        IAnchor Anchor { get; }
        IWriteAttachments Attachments { get; }
        T Load<T>(string id) where T : class;
        List<T> Query<T>() where T : class;
        void Store(object item);
        void Delete(object item);
    }
}