using System;
using OctoDB.Diagnostics;
using OctoDB.Storage;

namespace OctoDB
{
    public interface IDocumentStore : IDisposable
    {
        IStatistics Statistics { get; }
        IReadSession OpenReadSession();
        IReadSession OpenReadSession(string commitSha);
        IReadSession OpenReadSession(IAnchor anchor);
        WriteSession OpenWriteSession();
    }
}