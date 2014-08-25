using System;
using System.IO;

namespace OctoDB.Storage
{
    public class StoredFile
    {
        readonly string name;
        readonly string path;
        readonly string sha;
        readonly Func<Stream> getContents;

        public StoredFile(string name, string path, string sha, Func<Stream> getContents)
        {
            this.name = name;
            this.path = path;
            this.sha = sha;
            this.getContents = getContents;
        }

        public string Name
        {
            get { return name; }
        }

        public string Path
        {
            get { return path; }
        }

        public string Sha
        {
            get { return sha; }
        }

        public Stream GetContents()
        {
            return getContents();
        }
    }
}