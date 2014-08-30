using System;
using System.Collections.Generic;
using System.Runtime.Remoting.Activation;
using LibGit2Sharp;
using OctoDB.Diagnostics;

namespace OctoDB.Storage
{
    public interface IStorageEngine : IDisposable
    {
        bool IsRepositoryEmpty { get; }
        IAnchor GetCurrentAnchor();
        IAnchor GetAnchor(string sha);
        List<IAnchor> GetAnchors();
        void Visit(IAnchor anchor, IStorageVisitor visitor);
        IStorageBatch Batch();
    }
}