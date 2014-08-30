using System;
using System.IO;

namespace OctoDB.Storage
{
    public class StoredFile
    {
        readonly string path;
        readonly string sha;
        readonly Func<Stream> getContents;

        public StoredFile(string path, string sha, Func<Stream> getContents)
        {
            this.path = path;
            this.sha = sha;
            this.getContents = getContents;
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