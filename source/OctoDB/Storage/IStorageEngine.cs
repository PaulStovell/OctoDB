using System;
using System.Collections.Generic;
using OctoDB.Diagnostics;

namespace OctoDB.Storage
{
    public interface IStorageEngine : IDisposable
    {
        IAnchor GetCurrentAnchor();

        Dictionary<string, object> LoadSnapshot(IAnchor anchor, IAnchor previous);
        List<T> LoadAll<T>(IAnchor anchor);
        T Load<T>(IAnchor anchor, string id);
        IStorageBatch Batch();
        IStatistics Statistics { get; }
    }
}