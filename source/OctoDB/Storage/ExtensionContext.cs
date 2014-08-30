namespace OctoDB.Storage
{
    public class ExtensionContext
    {
        public ExtensionContext(IWriteSession session, IAnchor anchor, IStorageEngine engine)
        {
            Session = session;
            Anchor = anchor;
            Engine = engine;
        }

        public IWriteSession Session { get; private set; }
        public IAnchor Anchor { get; private set; }
        public IStorageEngine Engine { get; private set; }
    }
}