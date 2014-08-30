using System;
using System.Collections.ObjectModel;
using OctoDB.Storage;

namespace OctoDB
{
    public interface IReadSession : IDisposable
    {
        IReadAttachments Attachments { get; }
        IAnchor Anchor { get; }
        T Load<T>(string id) where T : class;
        ReadOnlyCollection<T> Query<T>() where T : class;
    }
}