using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using LibGit2Sharp;
using NUnit.Framework;
using OctoDB.Storage;
using OctoDB.Tests.Fixtures;
using OctoDB.Tests.SampleModel;

namespace OctoDB.Tests
{
    [TestFixture]
    public class Usage : StorageFixture
    {
        [Test]
        public void WriteSession_CanStoreAndLoadById()
        {
            using (var session = Store.OpenWriteSession())
            {
                session.Store(new Project { Id = "acme", Name = "ACME Web" });
                session.Commit("Added a project");
            }

            using (var session = Store.OpenWriteSession())
            {
                var project = session.Load<Project>("acme");
                Assert.That(project.Name, Is.EqualTo("ACME Web"));
            }
        }

        [Test]
        public void WriteSession_CanQuery()
        {
            using (var session = Store.OpenWriteSession())
            {
                session.Store(new Project { Id = "acme-1", Name = "ACME 1" });
                session.Store(new Project { Id = "acme-2", Name = "ACME 2" });
                session.Commit("Added projects");
            }

            using (var session = Store.OpenWriteSession())
            {
                var projects = session.Query<Project>();
                Assert.That(projects.Count, Is.EqualTo(2));
                Assert.That(projects[0].Name, Is.EqualTo("ACME 1"));
                Assert.That(projects[1].Name, Is.EqualTo("ACME 2"));
            }
        }

        [Test]
        public void WriteSession_CanDelete()
        {
            using (var session = Store.OpenWriteSession())
            {
                session.Store(new Project { Id = "acme", Name = "ACME Web" });
                session.Commit("Added a project");
            }

            using (var session = Store.OpenWriteSession())
            {
                var project = session.Load<Project>("acme");
                Assert.That(project, Is.Not.Null);
                session.Delete(project);
                session.Commit("Deleted a project");
            }

            using (var session = Store.OpenWriteSession())
            {
                var project = session.Load<Project>("acme");
                Assert.That(project, Is.Null);

                var projects = session.Query<Project>();
                Assert.That(projects.Count, Is.EqualTo(0));
            }
        }

        [Test]
        public void ReadOnlySessionsReuseInstances()
        {
            using (var session = Store.OpenWriteSession())
            {
                session.Store(new Project { Id = "acme-1", Description = "A" });
                session.Commit("Added project 1");
            }

            Project projectA;
            using (var session = Store.OpenReadSession())
                projectA = session.Load<Project>("acme-1");
            
            Project projectB;
            using (var session = Store.OpenReadSession())
                projectB = session.Load<Project>("acme-1");
            
            Assert.AreEqual(projectA, projectB);
            Assert.That(projectB.Description, Is.EqualTo("A"));

            using (var session = Store.OpenWriteSession())
            {
                var project = session.Load<Project>("acme-1");
                project.Description = "B";
                session.Store(project);
                session.Commit("Changed project 1");
            }
            
            Project projectC;
            using (var session = Store.OpenReadSession())
                projectC = session.Load<Project>("acme-1");

            Assert.AreNotEqual(projectC, projectB);
            Assert.That(projectC.Description, Is.EqualTo("B"));
        }

        [Test]
        public void CanStoreAttachments()
        {
            using (var session = Store.OpenWriteSession())
            {
                session.Attachments.Store("foo\\bar\\baz.bin", new byte[] { 42, 43, 44 });
                session.Attachments.Store("foo\\hello.md", "Hello **world**!\r\n");
                session.Commit("Added attachments");
            }

            // Attachments are loaded into memory for read sessions
            using (var session = Store.OpenReadSession())
            {
                var bytes = session.Attachments.Load("foo\\bar\\baz.bin");
                Assert.That(bytes.Length, Is.EqualTo(3));
                Assert.That(bytes[0], Is.EqualTo(42));
                Assert.That(bytes[1], Is.EqualTo(43));
                Assert.That(bytes[2], Is.EqualTo(44));

                var text = session.Attachments.LoadText("foo\\hello.md");
                Assert.That(text, Is.EqualTo("Hello **world**!\r\n"));
            }

            // And also available in write sessions
            using (var session = Store.OpenWriteSession())
            {
                var bytes = session.Attachments.Load("foo\\bar\\baz.bin");
                Assert.That(bytes.Length, Is.EqualTo(3));
            }

            using (var session = Store.OpenWriteSession())
            {
                session.Attachments.Delete("foo\\bar\\baz.bin");
                session.Commit("Deleted an attachment");
            }

            using (var session = Store.OpenWriteSession())
            {
                var bytes = session.Attachments.Load("foo\\bar\\baz.bin");
                Assert.That(bytes, Is.Null);
            }
        }

        [Test]
        public void CanUseStore()
        {
            using (var session = Store.OpenWriteSession())
            {
                for (var i = 0; i < 50; i++)
                {
                    var project = new Project
                    {
                        Id = "acme-" + i,
                        Name = "My project 2",
                        Description = "Foo"
                    };

                    session.Store(project);
                }

                session.Commit("Added 50 projects");
            }

            var watch = Stopwatch.StartNew();
            var count = 0;
            while (watch.ElapsedMilliseconds < 5000)
            {
                using (var session = Store.OpenReadSession())
                {
                    var projects = session.Query<Project>();
                    var project = session.Load<Project>("acme-13");
                    count += projects.Count;
                }
            }

            Trace.WriteLine("Loaded: " + count);
        }

        [Test]
        public void RepoTest()
        {
            Repository.Init("Hello");
            DoCommit();
            DoCommit();
            DoCommit();
            DoCommit();
        }

        int commitNumber;

        void DoCommit()
        {
            commitNumber++;
            using (var repo = new Repository("Hello"))
            {
                var content = "Hello commit! " + Guid.NewGuid();

                var parents = new Commit[0];
                var treeDefinition = new TreeDefinition();
                if (repo.Head.Tip != null)
                {
                    treeDefinition = TreeDefinition.From(repo.Head.Tip);
                    parents = new[] { repo.Head.Tip };
                }

                var newBlob = repo.ObjectDatabase.CreateBlob(new MemoryStream(Encoding.UTF8.GetBytes(content)));

                treeDefinition.Add("filePath.txt", newBlob, Mode.NonExecutableFile);
                
                var tree = repo.ObjectDatabase.CreateTree(treeDefinition);
                var committer = new Signature("James", "@jugglingnutcase", DateTime.Now);
                var author = committer;
                var commit = repo.ObjectDatabase.CreateCommit(
                    author,
                    committer,
                    "Commit " + commitNumber,
                    tree, parents, false);

                var master = repo.Branches.FirstOrDefault(b => b.Name == "master");
                if (master == null)
                {
                    master = repo.Branches.Add("master", commit);
                }

                // Update the HEAD reference to point to the latest commit
                repo.Refs.UpdateTarget(master.CanonicalName, commit.Id.ToString());

                repo.Reset(ResetMode.Hard, commit);
            }
        }
    }
}
