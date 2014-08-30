namespace OctoDB.Storage
{
    public interface IWriteSessionExtension
    {
        void AfterOpen(ExtensionContext context);
        void BeforeStore(object document, ExtensionContext context);
        void AfterStore(object document, ExtensionContext context);
        void BeforeDelete(object document, ExtensionContext context);
        void AfterDelete(object document, ExtensionContext context);
        void BeforeCommit(IStorageBatch batch, ExtensionContext context);
        void AfterCommit(ExtensionContext context);
    }
}