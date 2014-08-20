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
    public class StorageEngine : IDisposable
    {
        readonly string rootPath;
        readonly Repository repository;
        readonly JsonSerializer serializer = new JsonSerializer { Formatting = Formatting.Indented };
        readonly ReaderWriterLockSlim sync = new ReaderWriterLockSlim();

        public StorageEngine(string rootPath)
        {
            serializer.ContractResolver = new GitDbContractResolver();
            this.rootPath = rootPath;

            Repository.Init(rootPath);

            repository = new Repository(rootPath);

            if (repository.Head.Tip == null) return;
            using (PerformanceCounters.GitReset())
            {
                repository.Reset(ResetMode.Hard);
            }
        }

        public List<HistoryEntry> History(string id)
        {
            sync.EnterReadLock();
            try
            {
                var workingName = id.Trim('\\', '/').Replace("/", "\\");
                var path = Path.Combine(rootPath, workingName + ".json");

                var parent = Path.GetDirectoryName(path);
                if (parent != null && !Directory.Exists(parent))
                {
                    Directory.CreateDirectory(parent);
                }

                if (!File.Exists(path))
                    return new List<HistoryEntry>();

                var entries = new List<HistoryEntry>();
                foreach (var commit in repository.Head.Commits)
                {
                    var found = TreeContainsFile(commit.Tree, workingName + ".json");
                    if (found != null)
                    {
                        var entry = new HistoryEntry()
                        {
                            Id = workingName + ".json",
                            Hash = commit.Sha,
                            Modified = commit.Author.When
                        };
                        entries.Add(entry);
                    }
                }

                return entries;
            }
            finally
            {
                sync.ExitReadLock();
            }
        }

        public T LoadAt<T>(string id, string hash)
        {
            sync.EnterReadLock();
            try
            {
                var workingName = id.Trim('\\', '/').Replace("/", "\\");
                var commit = repository.Head.Commits.First(c => c.Sha == hash);
                var fileBlob = TreeContainsFile(commit.Tree, workingName + ".json");

                using (PerformanceCounters.Deserialization())
                using (var streamReader = new StreamReader(fileBlob.GetContentStream(), Encoding.UTF8))
                using (var jsonReader = new JsonTextReader(streamReader))
                {
                    var context =
                        new GitDbSerializationContext(
                            delegate(object owner, string propertyName, ExternalAttribute attribute)
                            {
                                var key = Path.GetFileName(workingName) +
                                          (owner is T
                                              ? ""
                                              : "." + owner.GetType().GetProperty("Id").GetValue(owner, null)) + "." +
                                          attribute.Name;
                                var attachmentWorkingPath = Path.Combine(Path.GetDirectoryName(workingName), key);

                                var attachmentBlob = TreeContainsFile(commit.Tree, attachmentWorkingPath);
                                using (
                                    var attachmentStreamReader = new StreamReader(attachmentBlob.GetContentStream(),
                                        Encoding.UTF8))
                                {
                                    return attachmentStreamReader.ReadToEnd();
                                }
                            });

                    serializer.Context = new StreamingContext(serializer.Context.State, context);
                    var item = serializer.Deserialize<T>(jsonReader);

                    return item;
                }
            }
            finally
            {
                sync.ExitReadLock();
            }
        }

        private Blob TreeContainsFile(Tree tree, string filename)
        {
            if (tree.All(x => x.Path != filename))
                return
                    tree.Where(x => x.TargetType == TreeEntryTargetType.Tree)
                        .Select(x => x.Target as Tree)
                        .Select(branch => TreeContainsFile(branch, filename))
                        .FirstOrDefault(found => found != null);

            var o = tree.First(x => x.Path == filename);
            return (Blob)o.Target;
        }

        public T Load<T>(string id) where T : class
        {
            //sync.EnterReadLock();
            try
            {
                var workingName = id.Trim('\\', '/');
                var path = Path.Combine(rootPath, workingName + ".json");

                var parent = Path.GetDirectoryName(path);
                if (parent != null && !Directory.Exists(parent))
                {
                    Directory.CreateDirectory(parent);
                }

                if (!File.Exists(path))
                {
                    return null;
                }

                using (PerformanceCounters.Deserialization())
                using (var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var streamReader = new StreamReader(stream, Encoding.UTF8))
                using (var jsonReader = new JsonTextReader(streamReader))
                {
                    var context =
                        new GitDbSerializationContext(
                            delegate(object owner, string propertyName, ExternalAttribute attribute)
                            {
                                var key = Path.GetFileName(workingName) +
                                          (owner is T
                                              ? ""
                                              : "." + owner.GetType().GetProperty("Id").GetValue(owner, null)) + "." +
                                          attribute.Name;
                                var attachmentWorkingPath = Path.Combine(Path.GetDirectoryName(workingName), key);
                                var attachmentPath = Path.Combine(rootPath, attachmentWorkingPath);
                                if (!File.Exists(attachmentPath))
                                {
                                    return null;
                                }

                                using (
                                    var attachmentStream = File.Open(attachmentPath, FileMode.Open, FileAccess.Read,
                                        FileShare.Read))
                                using (var attachmentStreamReader = new StreamReader(attachmentStream, Encoding.UTF8))
                                {
                                    return attachmentStreamReader.ReadToEnd();
                                }
                            });

                    serializer.Context = new StreamingContext(serializer.Context.State, context);
                    var item = serializer.Deserialize<T>(jsonReader);

                    return item;
                }
            }
            finally
            {
                //sync.ExitReadLock();
            }
        }

        public List<T> LoadAll<T>(string prefix) where T : class
        {
            sync.EnterReadLock();
            try
            {
                var root = Path.Combine(rootPath, prefix);
                if (!Directory.Exists(root))
                {
                    return new List<T>();
                }

                var results = new List<T>();
                var files = Directory.GetFiles(root, "*.json", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    results.Add(Load<T>(file.Substring(rootPath.Length + 1).Replace(".json", "")));
                }

                return results;
            }
            finally
            {
                sync.ExitReadLock();
            }
        }

        public IStorageBatch Batch()
        {
            sync.EnterWriteLock();
            
            {
                var newBatch = new StorageBatch(repository, rootPath, serializer, delegate
                {
                    sync.ExitWriteLock();
                });
                newBatch.Prepare();
                return newBatch;
            }
        }

        class StorageBatch : IStorageBatch
        {
            private readonly Repository repository;
            private readonly string rootPath;
            private readonly JsonSerializer serializer;
            private readonly Action released;
            private readonly HashSet<string> filesToStage = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            private readonly HashSet<string> filesToDelete = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            public StorageBatch(Repository repository, string rootPath, JsonSerializer serializer, Action released)
            {
                this.repository = repository;
                this.rootPath = rootPath;
                this.serializer = serializer;
                this.released = released;
            }

            public void Prepare()
            {
                if (repository.Head.Tip == null)
                    return;

                using (PerformanceCounters.GitReset())
                {
                    repository.Reset(ResetMode.Hard);
                }
            }

            public void Put(string uri, object document)
            {
                var workingName = uri.Trim(new[] { '/', '\\' }).Replace("/", "\\");
                var workingPath = workingName + ".json";
                var path = Path.Combine(rootPath, workingPath);
                var parent = Path.GetDirectoryName(path);
                if (parent != null && !Directory.Exists(parent))
                {
                    Directory.CreateDirectory(parent);
                }

                List<AttachmentFoundEvent> attachments;

                var existingFiles = Directory.GetFiles(parent, Path.GetFileName(workingName) + ".*");

                using (PerformanceCounters.Serialization())
                {
                    using (var stream = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Read))
                    using (var streamWriter = new StreamWriter(stream, Encoding.UTF8))
                    using (var writer = new JsonTextWriter(streamWriter))
                    {
                        var context = new GitDbSerializationContext(null);
                        serializer.Context = new StreamingContext(serializer.Context.State, context);
                        serializer.Serialize(writer, document);
                        attachments = context.Attachments;
                        writer.Flush();
                    }
                }

                filesToStage.Add(workingPath);

                foreach (var attachment in attachments)
                {
                    string attachmentWorkingPath;

                    using (PerformanceCounters.Attachments())
                    {
                        var owner = attachment.Owner;
                        var key = Path.GetFileName(workingName) + (owner == document ? "" : "." + owner.GetType().GetProperty("Id").GetValue(owner, null)) + "." + attachment.Attribute.Name;
                        attachmentWorkingPath = Path.Combine(Path.GetDirectoryName(workingName), key);
                        var attachmentPath = Path.Combine(rootPath, attachmentWorkingPath);
                        using (var stream = File.Open(attachmentPath, FileMode.Create, FileAccess.Write, FileShare.Read))
                        using (var streamWriter = new StreamWriter(stream, Encoding.UTF8))
                        {
                            streamWriter.Write((string)attachment.Value);
                            streamWriter.Flush();
                        }
                    }

                    filesToStage.Add(attachmentWorkingPath);
                }

                foreach (var file in existingFiles)
                {
                    var relative = file.Substring(rootPath.Length + 1);
                    if (filesToStage.Contains(relative))
                        continue;

                    File.Delete(file);
                    filesToDelete.Add(relative);
                }
            }

            public void Delete(string uri)
            {
                var workingPath = uri.TrimStart(new[] { '/', '\\' }).Replace("/", "\\") + ".json";
                var path = Path.Combine(rootPath, workingPath);
                var parent = Path.GetDirectoryName(path);
                if (parent != null && !Directory.Exists(parent))
                {
                    return;
                }

                repository.Index.Remove(workingPath);
            }

            public void Commit(string message)
            {
                using (PerformanceCounters.GitStaging())
                {
                    filesToDelete.ExceptWith(filesToStage);

                    if (filesToDelete.Count > 0)
                    {
                        repository.Index.Remove(filesToDelete, false);
                    }

                    if (filesToStage.Count > 0)
                    {
                        repository.Index.Stage(filesToStage);
                    }

                    var status = repository.Index.RetrieveStatus();
                    if (!status.IsDirty)
                    {
                        return;
                    }
                }

                using (PerformanceCounters.GitCommit())
                {
                    repository.Commit(message, new Signature("Paul", "paul@paulstovell.com", DateTimeOffset.UtcNow));
                }
            }

            public void Dispose()
            {
                using (PerformanceCounters.GitReset())
                {
                    repository.Reset(ResetMode.Hard);
                }

                if (released != null) released();
            }
        }

        public void Dispose()
        {
            repository.Dispose();
        }
    }
}