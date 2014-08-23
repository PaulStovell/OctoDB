namespace OctoDB.Storage
{
    public interface IWriteableSession : IReadOnlySession
    {
        void Store(object item);
        void Delete(object item);
    }
}