using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using LibGit2Sharp;
using Newtonsoft.Json;
using OctoDB.Diagnostics;

namespace OctoDB.Storage
{
    public class StorageEngine : IStorageEngine
    {
        readonly Repository repository;
        readonly JsonSerializer serializer = new JsonSerializer { Formatting = Formatting.Indented };
        readonly ReaderWriterLockSlim sync = new ReaderWriterLockSlim();
        readonly IStatistics statistics = new Statistics();

        public StorageEngine(string rootPath)
        {
            serializer.ContractResolver = new OctoDbContractResolver();
            
            Repository.Init(rootPath);

            repository = new Repository(rootPath);
            if (repository.Head.Tip == null)
            {
                var blob = repository.ObjectDatabase.CreateBlob(new MemoryStream(Encoding.UTF8.GetBytes("* text=auto")));
                var definition = new TreeDefinition();
                definition.Add(".gitattributes", blob, Mode.NonExecutableFile);
                var tree = repository.ObjectDatabase.CreateTree(definition);
                var commit = repository.ObjectDatabase.CreateCommit(
                    new Signature("paul", "paul@paulstovell.com", DateTimeOffset.UtcNow),
                    new Signature("paul", "paul@paulstovell.com", DateTimeOffset.UtcNow),
                    "Initialize empty repository",
                    tree,
                    new Commit[0],
                    false);

                repository.Refs.UpdateTarget(repository.Refs.Head, commit.Id);
            }
        }

        public IStatistics Statistics { get { return statistics; } }

        public IAnchor GetCurrentAnchor()
        {
            sync.EnterReadLock();
            try
            {
                var tip = repository.Head.Tip;
                return new GitSnapshotAnchor(tip);
            }
            finally
            {
                sync.ExitReadLock();
            }
        }

        public Dictionary<string, object> LoadSnapshot(IAnchor anchor, IAnchor previous)
        {
            var currentGit = (GitSnapshotAnchor)anchor;
            var previousGit = (GitSnapshotAnchor)previous;

            if (previousGit != null)
            {
                foreach (var pair in previousGit.DocumentsById)
                {
                    currentGit.DocumentsById[pair.Key] = pair.Value;
                }

                foreach (var pair in previousGit.DocumentBlobShas)
                {
                    currentGit.DocumentBlobShas[pair.Key] = pair.Value;
                }
            }

            if (previousGit != null && currentGit.Id == previousGit.Id)
            {
                statistics.IncrementSnapshotReuse();
                return currentGit.DocumentsById;
            }
            
            statistics.IncrementSnapshotRebuild();

            var seen = new HashSet<string>();
            
            var loaded = new List<object>();
            LoadTree(currentGit, entry => true, (entry, type) =>
            {
                seen.Add(entry.Path);
                return true;
            }, loaded);

            var remove = new HashSet<string>();
            foreach (var path in currentGit.DocumentsById.Keys)
            {
                if (!seen.Contains(path))
                {
                    remove.Add(path);
                }
            }

            foreach (var item in remove)
            {
                currentGit.DocumentsById.Remove(item);
                currentGit.DocumentBlobShas.Remove(item);
            }

            return currentGit.DocumentsById;
        }

        public List<T> LoadAll<T>(IAnchor anchor)
        {
            var parentPath = Conventions.GetParentPath(typeof (T));

            var loaded = new List<object>();
            LoadTree(anchor, entry => entry.Path.Replace("\\", "/").StartsWith(parentPath), (entry, type) => true, loaded);
            return loaded.OfType<T>().ToList();
        }

        public T Load<T>(IAnchor anchor, string id)
        {
            var parentPath = Conventions.GetPath(typeof(T), id);

            var loaded = new List<object>();
            LoadTree(anchor, entry =>
            {
                return parentPath.StartsWith(entry.Path);
            }, (entry, type) => entry.Path == parentPath, loaded);
            return loaded.OfType<T>().FirstOrDefault();
        }

        void LoadTree(IAnchor anchor, Func<TreeEntry, bool> traverseFilter, Func<TreeEntry, Type, bool> loadFilter, List<object> loaded)
        {
            var gitReference = anchor as GitSnapshotAnchor;
            if (gitReference == null) throw new InvalidOperationException("Not a valid time reference");
            if (gitReference.Commit == null)
            {
                return;
            }

            sync.EnterReadLock();
            try
            {
                var tree = gitReference.Commit.Tree;
                LoadTree(gitReference, tree, traverseFilter, loadFilter, loaded);
            }
            finally
            {
                sync.ExitReadLock();
            }
        }

        void LoadTree(GitSnapshotAnchor anchor, Tree tree, Func<TreeEntry, bool> traverseFilter, Func<TreeEntry, Type, bool> loadFilter, List<object> loaded)
        {
            foreach (var entry in tree)
            {
                if (entry.TargetType == TreeEntryTargetType.Tree)
                {
                    var child = entry.Target as Tree;
                    if (child != null)
                    {
                        if (traverseFilter(entry))
                        {
                            LoadTree(anchor, child, traverseFilter, loadFilter, loaded);                            
                        }
                    }
                } 
                else if (entry.TargetType == TreeEntryTargetType.Blob)
                {
                    var type = Conventions.GetType(entry.Path);
                    if (type == null)
                        continue;

                    if (loadFilter(entry, type))
                    {
                        var blob = entry.Target as Blob;
                        var instance = Load(anchor, blob, entry, tree, type);
                        if (instance != null)
                        {
                            loaded.Add(instance);                            
                        }
                    }
                }
            }
        }

        object Load(GitSnapshotAnchor anchor, Blob fileBlob, TreeEntry entry, Tree parent, Type type)
        {
            var fileName = entry.Name;

            object existing;
            if (anchor.DocumentsById.TryGetValue(entry.Path, out existing) && anchor.DocumentBlobShas[entry.Path] == fileBlob.Sha)
            {
                return existing;
            }

            using (statistics.MeasureDeserialization())
            using (var streamReader = new StreamReader(fileBlob.GetContentStream(new FilteringOptions(entry.Path)), Encoding.UTF8))
            using (var jsonReader = new JsonTextReader(streamReader))
            {
                var context =
                    new OctoDbSerializationContext(
                        delegate(object owner, string propertyName, ExternalAttribute attribute)
                        {
                            using (statistics.MeasureAttachments())
                            {
                                var key = Path.GetFileNameWithoutExtension(fileName) + (type.IsInstanceOfType(owner) ? "" : "." + owner.GetType().GetProperty("Id").GetValue(owner, null)) + "." + attribute.Name;
                                var attachmentEntry = parent.FirstOrDefault(n => n.Name == key);
                                if (attachmentEntry == null) return null;
                                var attachmentBlob = attachmentEntry.Target as Blob;
                                if (attachmentBlob == null) return null;

                                using (var attachmentStreamReader = new StreamReader(attachmentBlob.GetContentStream(new FilteringOptions(attachmentEntry.Path)), Encoding.UTF8))
                                {
                                    return attachmentStreamReader.ReadToEnd();
                                }
                            }
                        });

                serializer.Context = new StreamingContext(serializer.Context.State, context);
                var result = serializer.Deserialize(jsonReader, type);
                statistics.IncrementLoaded();
                anchor.DocumentsById[entry.Path] = result;
                anchor.DocumentBlobShas[entry.Path] = fileBlob.Sha;
                return result;
            }
        }

        public IStorageBatch Batch()
        {
            sync.EnterWriteLock();
            {
                var newBatch = new StorageBatch(repository, statistics, serializer, delegate
                {
                    sync.ExitWriteLock();
                });
                newBatch.Prepare();
                return newBatch;
            }
        }

        public void Dispose()
        {
            sync.EnterWriteLock();
            try
            {
                repository.Dispose();
            }
            finally
            {
                sync.ExitWriteLock();
            }
        }

        class StorageBatch : IStorageBatch
        {
            private readonly Repository repository;
            readonly IStatistics statistics;
            private readonly JsonSerializer serializer;
            private readonly Action released;
            TreeDefinition treeDefinition;
            Commit parent;

            public StorageBatch(Repository repository, IStatistics statistics, JsonSerializer serializer, Action released)
            {
                this.repository = repository;
                this.statistics = statistics;
                this.serializer = serializer;
                this.released = released;
            }

            public void Prepare()
            {
                parent = repository.Head.Tip;
                if (parent == null)
                {
                    treeDefinition = new TreeDefinition();
                }
                else
                {
                    treeDefinition = TreeDefinition.From(parent.Tree);
                }
            }

            public void Put(object document)
            {
                using (statistics.MeasureGitStaging())
                {
                    var uri = Conventions.GetPath(document.GetType(), document);

                    List<AttachmentFoundEvent> attachments;

                    using (statistics.MeasureSerialization())
                    {
                        using (var stream = new MemoryStream())
                        using (var streamWriter = new StreamWriter(stream, Encoding.UTF8))
                        using (var writer = new JsonTextWriter(streamWriter))
                        {
                            var context = new OctoDbSerializationContext(null);
                            serializer.Context = new StreamingContext(serializer.Context.State, context);
                            serializer.Serialize(writer, document, typeof (object));
                            attachments = context.Attachments;
                            streamWriter.Flush();

                            stream.Seek(0, SeekOrigin.Begin);

                            var blob = repository.ObjectDatabase.CreateBlob(stream, uri);
                            treeDefinition.Add(uri, blob, Mode.NonExecutableFile);
                        }
                    }

                    foreach (var attachment in attachments)
                    {
                        using (statistics.MeasureAttachments())
                        {
                            var owner = attachment.Owner;
                            var key = Path.Combine(Path.GetDirectoryName(uri), Path.GetFileNameWithoutExtension(uri)) + (owner == document ? "" : "." + owner.GetType().GetProperty("Id").GetValue(owner, null)) + "." + attachment.Attribute.Name;
                            if (attachment.Value != null)
                            {
                                var blob = repository.ObjectDatabase.CreateBlob(new MemoryStream(Encoding.UTF8.GetBytes((string)attachment.Value)), uri);
                                treeDefinition.Add(key, blob, Mode.NonExecutableFile);
                            }
                            else
                            {
                                treeDefinition.Remove(key);
                            }
                        }
                    }
                }
            }

            public void Delete(object document)
            {
                using (statistics.MeasureGitStaging())
                {
                    var uri = Conventions.GetPath(document.GetType(), document);
                    treeDefinition.Remove(uri);
                }
            }

            public void Commit(string message)
            {
                using (statistics.MeasureGitCommit())
                {
                    var tree = repository.ObjectDatabase.CreateTree(treeDefinition);

                    var parents = parent == null ? new Commit[0] : new[] {parent};

                    var commit = repository.ObjectDatabase.CreateCommit(
                        new Signature("paul", "paul@paulstovell.com", DateTimeOffset.UtcNow),
                        new Signature("paul", "paul@paulstovell.com", DateTimeOffset.UtcNow),
                        message,
                        tree,
                        parents,
                        false
                        );

                    repository.Refs.UpdateTarget(repository.Refs.Head, commit.Id);
                }
            }

            public void Dispose()
            {
                if (released != null) released();
            }
        }

        class GitSnapshotAnchor : IAnchor
        {
            readonly Commit commit;

            public GitSnapshotAnchor(Commit commit)
            {
                this.commit = commit;
                DocumentsById = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                DocumentBlobShas = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            public string Id { get { return Commit == null ? "" : Commit.Sha; } }
            public Commit Commit { get { return commit; } }
            public Dictionary<string, object> DocumentsById { get; private set; }
            public Dictionary<string, string> DocumentBlobShas { get; private set; }
        }
    }
}