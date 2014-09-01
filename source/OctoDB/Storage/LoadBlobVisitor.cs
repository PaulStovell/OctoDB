using System;
using System.Collections.Generic;
using System.Linq;

namespace OctoDB.Storage
{
    public class LoadBlobVisitor : IStorageVisitor
    {
        readonly HashSet<string> pathsToLoad;
        readonly DocumentSet documents;
        readonly IEncoderRegistry encoderRegistry;
        readonly Dictionary<string, byte[]> loaded = new Dictionary<string, byte[]>();

        public LoadBlobVisitor(IEnumerable<string> paths, DocumentSet documents, IEncoderRegistry encoderRegistry)
        {
            pathsToLoad = new HashSet<string>(paths, StringComparer.OrdinalIgnoreCase);
            this.documents = documents;
            this.encoderRegistry = encoderRegistry;
        }

        public Dictionary<string, byte[]> Loaded
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
                var document = documents.Load(file, DecodeFile) as byte[];
                if (document != null)
                {
                    loaded.Add(file.Path, document);
                }
            }
        }

        object DecodeFile(StoredFile file)
        {
            return encoderRegistry.Decode(file.Path, file.GetContents());
        }
    }
}