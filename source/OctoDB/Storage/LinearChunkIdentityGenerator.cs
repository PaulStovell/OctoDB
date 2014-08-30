namespace OctoDB.Storage
{
    public class LinearChunkIdentityGenerator : IWriteSessionExtension
    {
        static IdentityAllocations allocations; 

        public void AfterOpen(ExtensionContext context)
        {
            if (allocations == null)
            {
                allocations = context.Session.Load<IdentityAllocations>("ids") ?? new IdentityAllocations { Id = "ids" };
            }
        }

        public void BeforeStore(object document, ExtensionContext context)
        {
            object currentId = Conventions.GetId(document);

            if (currentId == null || (currentId is int && (int)currentId == 0))
            {
                var type = document.GetType().Name;
                currentId = allocations.Next(type);

                Conventions.AssignId(document, currentId);

                context.Session.Store(allocations);
            }
        }

        public void AfterStore(object document, ExtensionContext context)
        {

        }

        public void BeforeDelete(object document, ExtensionContext context)
        {

        }

        public void AfterDelete(object document, ExtensionContext context)
        {

        }

        public void BeforeCommit(IStorageBatch batch, ExtensionContext context)
        {
        }

        public void AfterCommit(ExtensionContext context)
        {
        }
    }
}