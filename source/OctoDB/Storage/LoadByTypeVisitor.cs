using System.Collections.Generic;
using System.IO;

namespace OctoDB.Storage
{
    public class LoadByTypeVisitor<T> : IStorageVisitor where T : class
    {
        readonly DocumentSet documents;
        readonly IEncoderRegistry encoderRegistry;
        readonly List<T> loaded = new List<T>();
        readonly string parentPath;

        public LoadByTypeVisitor(DocumentSet documents, IEncoderRegistry encoderRegistry)
        {
            this.documents = documents;
            this.encoderRegistry = encoderRegistry;
            parentPath = Conventions.GetParentPath(typeof (T));
        }

        public List<T> Loaded
        {
            get { return loaded; }
        } 

        public bool ShouldTraverseDirectory(string path)
        {
            return parentPath.StartsWith(path) || path.StartsWith(parentPath);
        }

        public void VisitDirectory(string path, IList<StoredFile> files)
        {
            foreach (var file in files)
            {
                if (Conventions.GetType(file.Path) == typeof (T))
                {
                    var document = documents.Load(file, DecodeFile) as T;
                    if (document != null)
                    {
                        loaded.Add(document);
                    }
                }
            }
        }

        object DecodeFile(StoredFile file)
        {
            return encoderRegistry.Decode(file.Path, file.GetContents());
        }
    }
}