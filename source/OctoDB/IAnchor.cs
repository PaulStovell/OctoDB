namespace OctoDB.Storage
{
    public interface IAnchor
    {
        string Id { get; }
        string Message { get; }
        CommitSignature Author { get; }
        CommitSignature Committer { get; }
    }
}