using System.Collections.Generic;

namespace OctoDB.Storage
{
    public interface IStorageVisitor
    {
        bool ShouldTraverseDirectory(string path);
        void VisitDirectory(string path, IList<StoredFile> files);
    }
}