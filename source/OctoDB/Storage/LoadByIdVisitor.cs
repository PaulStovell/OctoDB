using System;
using System.Collections.Generic;
using System.Linq;

namespace OctoDB.Storage
{
    public class LoadByIdVisitor<T> : IStorageVisitor where T : class
    {
        readonly HashSet<string> pathsToLoad;
        readonly DocumentSet documents;
        readonly IEncoderRegistry encoderRegistry;
        readonly List<T> loaded = new List<T>();

        public LoadByIdVisitor(IEnumerable<string> ids, DocumentSet documents, IEncoderRegistry encoderRegistry)
        {
            pathsToLoad = new HashSet<string>(ids.Select(id => Conventions.GetPath(typeof (T), (string) id)), StringComparer.OrdinalIgnoreCase);
            this.documents = documents;
            this.encoderRegistry = encoderRegistry;
        }

        public List<T> Loaded
        {
            get { return loaded; }
        } 

        public bool ShouldTraverseDirectory(string path)
        {
            return pathsToLoad.Any(p => (p ?? string.Empty).StartsWith(path));
        }

        public void VisitDirectory(string path, IList<StoredFile> files)
        {
            foreach (var file in files)
            {
                if (!pathsToLoad.Contains(file.Path)) continue;
                var document = documents.Load(file, DecodeFile) as T;
                if (document != null)
                {
                    loaded.Add(document);
                }
            }
        }

        object DecodeFile(StoredFile file)
        {
            return encoderRegistry.Decode(file.Path, file.GetContents());
        }
    }
}