using System;
using System.Runtime.Remoting.Activation;
using LibGit2Sharp;
using OctoDB.Diagnostics;

namespace OctoDB.Storage
{
    public interface IStorageEngine : IDisposable
    {
        bool IsRepositoryEmpty { get; }
        IAnchor GetCurrentAnchor();

        void Visit(IAnchor anchor, IStorageVisitor visitor);

        IStorageBatch Batch();
    }
}