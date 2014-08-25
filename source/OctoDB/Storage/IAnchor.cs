using System;

namespace OctoDB.Storage
{
    public interface IAnchor
    {
        string Id { get; }
        string Message { get; }
        CommitSignature Author { get; }
        CommitSignature Committer { get; }
    }

    public class CommitSignature
    {
        public CommitSignature(string name, string emailAddress, DateTimeOffset when)
        {
            
        }
    }
}