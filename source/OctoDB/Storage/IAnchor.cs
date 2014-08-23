using System.Collections.Generic;

namespace OctoDB.Storage
{
    public interface IAnchor
    {
        string Id { get; }
        Dictionary<string, object> DocumentsById { get; } 
    }
}