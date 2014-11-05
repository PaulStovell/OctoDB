using System;
using System.Diagnostics;
using System.IO;
using NUnit.Framework;
using OctoDB.Diagnostics;
using OctoDB.Storage;

namespace OctoDB.Tests.Fixtures
{
    public class StorageFixture
    {
        protected DocumentStore DocumentStore { get; set; }

        public const string CommitBranch = "master";

        protected CommitSignature CommitSign;

        [SetUp]
        public void SetUp()
        {
            var path = Path.Combine(Environment.CurrentDirectory, GetType().Name + "Test");
            EnsurePath(path);

            DocumentStore = new DocumentStore(path,CommitBranch);
            CommitSign = new CommitSignature("paul", "paul@paulstovell.com", DateTimeOffset.UtcNow);
        }

        [TearDown]
        public void TearDown()
        {
            DocumentStore.Statistics.Print();

            DocumentStore.Dispose();
        }

        static void EnsurePath(string path)
        {
            Trace.WriteLine("Test data folder: " + path);

            var directory = new DirectoryInfo(path);
            if (directory.Exists)
            {
                DeleteFileSystemInfo(directory);
            }
            directory.Create();
            
        }

        private static void DeleteFileSystemInfo(FileSystemInfo fileSystemInfo)
        {
            var directoryInfo = fileSystemInfo as DirectoryInfo;
            if (directoryInfo != null)
            {
                foreach (var childInfo in directoryInfo.GetFileSystemInfos())
                {
                    DeleteFileSystemInfo(childInfo);
                }
            }

            fileSystemInfo.Attributes = FileAttributes.Normal;
            fileSystemInfo.Delete();
        }
    }
}