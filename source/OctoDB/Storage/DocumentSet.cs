using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace OctoDB.Storage
{
    public class ExtensionContext
    {
        public ExtensionContext(IWriteableSession session, IAnchor anchor, IStorageEngine engine)
        {
            Session = session;
            Anchor = anchor;
            Engine = engine;
        }

        public IWriteableSession Session { get; private set; }
        public IAnchor Anchor { get; private set; }
        public IStorageEngine Engine { get; private set; }
    }

    public interface IWriteSessionExtension
    {
        void AfterOpen(ExtensionContext context);
        void BeforeStore(object document, ExtensionContext context);
        void AfterStore(object document, ExtensionContext context);
        void BeforeDelete(object document, ExtensionContext context);
        void AfterDelete(object document, ExtensionContext context);
        void BeforeCommit(IStorageBatch batch, ExtensionContext context);
        void AfterCommit(ExtensionContext context);
    }

    // {
    //   'Project': 100,
    //   'Environment: 50,

    [Document("meta\\{id}.json")]
    public class IdentityAllocations
    {
        public IdentityAllocations()
        {
            NextIdentity = new ConcurrentDictionary<string, int>();
        }

        public string Id { get; set; }

        public ConcurrentDictionary<string, int> NextIdentity { get; private set; }

        public int Next(string collection)
        {
            return NextIdentity.AddOrUpdate(collection, c => 1, (c, i) => i + 1);
        }
    }

    public class LinearChunkIdentityGenerator : IWriteSessionExtension
    {
        static IdentityAllocations allocations; 

        public void AfterOpen(ExtensionContext context)
        {
            if (allocations == null)
            {
                allocations = context.Session.Load<IdentityAllocations>("ids") ?? new IdentityAllocations { Id = "ids" };
            }
        }

        public void BeforeStore(object document, ExtensionContext context)
        {
            object currentId = Conventions.GetId(document);

            if (currentId == null || (currentId is int && (int)currentId == 0))
            {
                var type = document.GetType().Name;
                currentId = allocations.Next(type);

                Conventions.AssignId(document, currentId);

                context.Session.Store(allocations);
            }
        }

        public void AfterStore(object document, ExtensionContext context)
        {

        }

        public void BeforeDelete(object document, ExtensionContext context)
        {

        }

        public void AfterDelete(object document, ExtensionContext context)
        {

        }

        public void BeforeCommit(IStorageBatch batch, ExtensionContext context)
        {
        }

        public void AfterCommit(ExtensionContext context)
        {
        }
    }

    public class DocumentSet
    {
        readonly IDocumentEncoder encoder;
        readonly IAnchor anchor;
        readonly Dictionary<string, object> documentsByPath = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string, string> documentShas = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        readonly ReaderWriterLockSlim sync = new ReaderWriterLockSlim();

        public DocumentSet(IDocumentEncoder encoder, IAnchor anchor)
        {
            this.encoder = encoder;
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

        public object Load(StoredFile file, IEnumerable<StoredFile> filesInDirectory)
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
                    var type = Conventions.GetType(file.Path);
                    if (type == null)
                        return null;

                    var read = encoder.Read(file.GetContents(), Conventions.GetType(file.Path), (attachmentKey, attachmentReader) =>
                    {
                        var attachment = filesInDirectory.FirstOrDefault(f => f.Name == attachmentKey);
                        if (attachment != null)
                        {
                            attachmentReader(attachment.GetContents());
                        }
                    });

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

        public void Add(object o)
        {
            sync.EnterWriteLock();
            try
            {
                if (o == null) return;

                var id = Conventions.GetId(o);
                if (id == null)
                    throw new ArgumentException(string.Format("An ID must be assigned to this {0}", o.GetType()));

                var path = Conventions.GetPath(o.GetType(), o);

                object existing;
                if (documentsByPath.TryGetValue(path, out existing) && existing != o)
                    throw new Exception(string.Format("An object with ID {0} already exists in this session", id));

                documentsByPath[path] = o;
                documentShas[path] = null;
            }
            finally
            {
                sync.ExitWriteLock();
            }
        }

        public void Evict(object o)
        {
            sync.EnterWriteLock();
            try
            {
                var id = Conventions.GetId(o);
                if (id == null)
                    throw new ArgumentException(string.Format("An ID must be assigned to this {0}", o.GetType()));

                var path = Conventions.GetPath(o.GetType(), o);
                if (documentsByPath.ContainsKey(path))
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