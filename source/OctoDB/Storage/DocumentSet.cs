using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace OctoDB.Storage
{
    public class DocumentSet
    {
        readonly IAnchor anchor;
        readonly Dictionary<string, object> documentsByPath = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string, string> documentShas = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        readonly ReaderWriterLockSlim sync = new ReaderWriterLockSlim();

        public DocumentSet(IAnchor anchor)
        {
            this.anchor = anchor;
        }

        public IAnchor Anchor
        {
            get { return anchor; }
        }

        public object Get(string path)
        {
            sync.EnterReadLock();
            try
            {
                object result;
                if (documentsByPath.TryGetValue(path, out result))
                {
                    return result;
                }
                return null;
            }
            finally
            {
                sync.ExitReadLock();
            }
        }

        public List<T> GetAll<T>()
        {
            sync.EnterReadLock();
            try
            {
                return documentsByPath.Values.OfType<T>().ToList();
            }
            finally
            {
                sync.ExitReadLock();
            }
        }

        public object Load(StoredFile file, Func<StoredFile, object> reader)
        {
            sync.EnterUpgradeableReadLock();

            try
            {
                object existing;
                if (documentsByPath.TryGetValue(file.Path, out existing) && documentShas[file.Path] == file.Sha)
                {
                    return existing;
                }

                sync.EnterWriteLock();
                try
                {
                    var read = reader(file);
                    documentsByPath[file.Path] = read;
                    documentShas[file.Path] = file.Sha;
                    return read;
                }
                finally
                {
                    sync.ExitWriteLock();
                }
            }
            finally
            {
                sync.ExitUpgradeableReadLock();
            }
        }

        public void Add(string path, object o, bool force)
        {
            sync.EnterWriteLock();
            try
            {
                if (o == null) return;

                if (!force)
                {
                    object existing;
                    if (documentsByPath.TryGetValue(path, out existing) && existing != o)
                        throw new IdentifierAlreadyInUseException(string.Format("An object at '{0}' already exists in this session", path));   
                }

                documentsByPath[path] = o;
                documentShas[path] = null;
            }
            finally
            {
                sync.ExitWriteLock();
            }
        }

        public void InitializeFrom(DocumentSet previousSet)
        {
            sync.EnterWriteLock();
            try
            {
                previousSet.sync.EnterReadLock();
                try
                {
                    foreach (var pair in previousSet.documentsByPath)
                    {
                        documentsByPath[pair.Key] = pair.Value;
                        documentShas[pair.Key] = previousSet.documentShas[pair.Key];
                    }
                }
                finally
                {
                    previousSet.sync.ExitReadLock();
                }
            }
            finally
            {
                sync.ExitWriteLock();
            }
        }

        public void RemoveExcept(HashSet<string> visited)
        {
            sync.EnterWriteLock();
            try
            {
                var remove = new HashSet<string>();
                foreach (var key in documentsByPath.Keys)
                {
                    if (!visited.Contains(key))
                    {
                        remove.Add(key);
                    }
                }

                foreach (var path in remove)
                {
                    documentsByPath.Remove(path);
                    documentShas.Remove(path);
                }
            }
            finally
            {
                sync.ExitWriteLock();
            }
        }
    }
}