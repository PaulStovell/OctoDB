using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using OctoDB.Diagnostics;

namespace OctoDB.Storage
{
    public class ReadOnlySession : IDisposable
    {
        readonly IStorageEngine storageEngine;
        readonly IAnchor anchor;
        readonly IDictionary<string, object> documentsById;
        readonly IDictionary<Type, IList> documentsByType = new Dictionary<Type, IList>();

        public ReadOnlySession(IStorageEngine storageEngine, IAnchor anchor, IDictionary<string, object> documentsById)
        {
            this.storageEngine = storageEngine;
            this.anchor = anchor;
            this.documentsById = documentsById;
        }

        public IAnchor Anchor { get { return anchor; } }

        public T Load<T>(string id) where T : class
        {
            var path = Conventions.GetPath(typeof (T), id);
            object result;
            if (documentsById.TryGetValue(path, out result))
            {
                return result as T;
            }

            return null;
        }

        public ReadOnlyCollection<T> Query<T>()
        {
            IList result;
            if (!documentsByType.TryGetValue(typeof (T), out result))
            {
                var documents = new ReadOnlyCollection<T>(documentsById.Values.OfType<T>().ToList());
                result = documents;
                documentsByType[typeof (T)] = documents;
            }

            return (ReadOnlyCollection<T>)result;
        }

        public void Dispose()
        {
            storageEngine.Statistics.IncrementReadSessionsClosed();
        }
    }
}