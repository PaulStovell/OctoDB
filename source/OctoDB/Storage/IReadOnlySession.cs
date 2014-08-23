using System;
using System.Collections.Generic;

namespace OctoDB.Storage
{
    public interface IReadOnlySession : IDisposable
    {
        T Load<T>(string id) where T : class;
        List<T> Query<T>() where T : class;
    }
}