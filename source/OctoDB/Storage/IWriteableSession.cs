namespace OctoDB.Storage
{
    public interface IWriteableSession : IReadOnlySession
    {
        IWriteAttachments Attachments { get; }
        void Store(object item);
        void Delete(object item);
    }
}