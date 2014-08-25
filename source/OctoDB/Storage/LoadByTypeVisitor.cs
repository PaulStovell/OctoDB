using System.Collections.Generic;

namespace OctoDB.Storage
{
    public class LoadByTypeVisitor<T> : IStorageVisitor where T : class
    {
        readonly DocumentSet documents;
        readonly List<T> loaded = new List<T>();
        readonly string parentPath;

        public LoadByTypeVisitor(DocumentSet documents)
        {
            this.documents = documents;
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
                if (Conventions.GetType(path) == typeof (T))
                {
                    var document = documents.Load(file, files) as T;
                    if (document != null)
                    {
                        loaded.Add(document);
                    }
                }
            }
        }
    }
}