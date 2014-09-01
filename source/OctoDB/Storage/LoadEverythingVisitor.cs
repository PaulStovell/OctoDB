using System.Collections.Generic;

namespace OctoDB.Storage
{
    public class LoadEverythingVisitor : IStorageVisitor
    {
        readonly DocumentSet set;
        readonly IEncoderRegistry encoderRegistry;
        readonly HashSet<string> visited;

        public LoadEverythingVisitor(DocumentSet set, IEncoderRegistry encoderRegistry, HashSet<string> visited)
        {
            this.set = set;
            this.encoderRegistry = encoderRegistry;
            this.visited = visited;
        }

        public bool ShouldTraverseDirectory(string path)
        {
            return true;
        }

        public void VisitDirectory(string path, IList<StoredFile> files)
        {
            foreach (var file in files)
            {
                visited.Add(file.Path);
                set.Load(file, DecodeFile);
            }
        }

        object DecodeFile(StoredFile file)
        {
            return encoderRegistry.Decode(file.Path, file.GetContents());
        }
    }
}