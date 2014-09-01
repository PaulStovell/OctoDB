using OctoDB.Storage;

namespace OctoDB
{
    public interface IAnchor
    {
        string Id { get; }
        string Message { get; }
        CommitSignature Author { get; }
        CommitSignature Committer { get; }
    }
}