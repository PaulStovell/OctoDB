using System.Collections.Concurrent;

namespace OctoDB.Storage
{
    [Document("meta\\{id}.json")]
    public class IdentityAllocations
    {
        public IdentityAllocations()
        {
            NextIdentity = new ConcurrentDictionary<string, int>();
        }

        public string Id { get; set; }

        public ConcurrentDictionary<string, int> NextIdentity { get; private set; }

        public int Next(string collection)
        {
            return NextIdentity.AddOrUpdate(collection, c => 1, (c, i) => i + 1);
        }
    }
}