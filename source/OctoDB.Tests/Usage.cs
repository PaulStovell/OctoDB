using System.Diagnostics;
using System.Linq;
using NUnit.Framework;
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
            using (var session = DocumentStore.OpenWriteSession())
            {
                session.Store(new Project { Id = "acme", Name = "ACME Web" });
                session.Commit("Added a project");
            }

            using (var session = DocumentStore.OpenWriteSession())
            {
                var project = session.Load<Project>("acme");
                Assert.That(project.Name, Is.EqualTo("ACME Web"));
            }
        }

        [Test]
        public void WriteSession_CanModify()
        {
            using (var session = DocumentStore.OpenWriteSession())
            {
                session.Store(new Project { Id = "acme", Name = "ACME Web" });
                session.Commit("Added a project");
            }

            using (var session = DocumentStore.OpenWriteSession())
            {
                var project = session.Load<Project>("acme");
                Assert.That(project.Name, Is.EqualTo("ACME Web"));
                project.Name = "ACME Server";
                session.Store(project);
                session.Commit("Renamed project");
            }

            using (var session = DocumentStore.OpenWriteSession())
            {
                var project = session.Load<Project>("acme");
                Assert.That(project.Name, Is.EqualTo("ACME Server"));
            }
        }

        [Test]
        public void WriteSession_StoreIsExplicit()
        {
            using (var session = DocumentStore.OpenWriteSession())
            {
                session.Store(new Project { Id = "acme", Name = "ACME Web" });
                session.Commit("Added a project");
            }

            using (var session = DocumentStore.OpenWriteSession())
            {
                var project = session.Load<Project>("acme");
                Assert.That(project.Name, Is.EqualTo("ACME Web"));
                project.Name = "ACME Server";
                // Forgot to call session.Store()
                session.Commit("Renamed project");
            }

            using (var session = DocumentStore.OpenWriteSession())
            {
                var project = session.Load<Project>("acme");
                Assert.That(project.Name, Is.EqualTo("ACME Web"));
            }
        }

        [Test]
        public void WriteSession_CanQuery()
        {
            using (var session = DocumentStore.OpenWriteSession())
            {
                session.Store(new Project { Id = "acme-1", Name = "ACME 1" });
                session.Store(new Project { Id = "acme-2", Name = "ACME 2" });
                session.Commit("Added projects");
            }

            using (var session = DocumentStore.OpenWriteSession())
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
            using (var session = DocumentStore.OpenWriteSession())
            {
                session.Store(new Project { Id = "acme", Name = "ACME Web" });
                session.Commit("Added a project");
            }

            using (var session = DocumentStore.OpenWriteSession())
            {
                var project = session.Load<Project>("acme");
                Assert.That(project, Is.Not.Null);
                session.Delete(project);
                session.Commit("Deleted a project");
            }

            using (var session = DocumentStore.OpenWriteSession())
            {
                var project = session.Load<Project>("acme");
                Assert.That(project, Is.Null);

                var projects = session.Query<Project>();
                Assert.That(projects.Count, Is.EqualTo(0));
            }
        }

        [Test]
        public void WriteSession_UsesIdentityMap()
        {
            using (var session = DocumentStore.OpenWriteSession())
            {
                session.Store(new Project { Id = "acme", Name = "ACME Web" });
                session.Commit("Added machines");
            }

            using (var session = DocumentStore.OpenWriteSession())
            {
                var a = session.Load<Project>("acme");
                var b = session.Load<Project>("acme");
                var c = session.Query<Project>().Single();

                Assert.AreEqual(a, b);
                Assert.AreEqual(b, c);
            }
        }

        [Test]
        public void WriteSession_CanAssignIds()
        {
            using (var session = DocumentStore.OpenWriteSession())
            {
                session.Store(new Machine { Name = "Web01" });
                session.Store(new Machine { Name = "Web02" });
                session.Commit("Added machines");
            }

            using (var session = DocumentStore.OpenWriteSession())
            {
                var machines = session.Query<Machine>();
                Assert.That(machines.Count, Is.EqualTo(2));
                Assert.That(machines[0].Id, Is.EqualTo(1));
                Assert.That(machines[1].Id, Is.EqualTo(2));
            }
        }

        [Test]
        public void WriteSession_CanStoreAttachments()
        {
            using (var session = DocumentStore.OpenWriteSession())
            {
                session.Attachments.Store("foo\\bar\\baz.bin", new byte[] { 42, 43, 44 });
                session.Attachments.Store("foo\\hello.md", "Hello **world**!\r\n");
                session.Commit("Added attachments");
            }

            // All attachments are loaded into memory for read sessions
            using (var session = DocumentStore.OpenReadSession())
            {
                var bytes = session.Attachments.LoadBinary("foo\\bar\\baz.bin");
                Assert.That(bytes.Length, Is.EqualTo(3));
                Assert.That(bytes[0], Is.EqualTo(42));
                Assert.That(bytes[1], Is.EqualTo(43));
                Assert.That(bytes[2], Is.EqualTo(44));

                var text = session.Attachments.LoadText("foo\\hello.md");
                Assert.That(text, Is.EqualTo("Hello **world**!\r\n"));
            }

            // And also available in write sessions
            using (var session = DocumentStore.OpenWriteSession())
            {
                var bytes = session.Attachments.LoadBinary("foo\\bar\\baz.bin");
                Assert.That(bytes.Length, Is.EqualTo(3));
            }

            using (var session = DocumentStore.OpenWriteSession())
            {
                session.Attachments.Delete("foo\\bar\\baz.bin");
                session.Commit("Deleted an attachment");
            }

            using (var session = DocumentStore.OpenWriteSession())
            {
                var bytes = session.Attachments.LoadBinary("foo\\bar\\baz.bin");
                Assert.That(bytes, Is.Null);
            }
        }

        [Test]
        public void ReadSession_ReusesInstances()
        {
            using (var session = DocumentStore.OpenWriteSession())
            {
                session.Store(new Project { Id = "acme-1", Description = "A" });
                session.Commit("Added project 1");
            }

            Project projectA;
            using (var session = DocumentStore.OpenReadSession())
                projectA = session.Load<Project>("acme-1");

            Project projectB;
            using (var session = DocumentStore.OpenReadSession())
                projectB = session.Load<Project>("acme-1");

            Assert.AreEqual(projectA, projectB);
            Assert.That(projectB.Description, Is.EqualTo("A"));

            using (var session = DocumentStore.OpenWriteSession())
            {
                var project = session.Load<Project>("acme-1");
                project.Description = "B";
                session.Store(project);
                session.Commit("Changed project 1");
            }

            Project projectC;
            using (var session = DocumentStore.OpenReadSession())
                projectC = session.Load<Project>("acme-1");

            Assert.AreNotEqual(projectC, projectB);
            Assert.That(projectC.Description, Is.EqualTo("B"));
        }

        [Test]
        public void ReadSessions_AreVeryFastWhenNoChanges()
        {
            using (var session = DocumentStore.OpenWriteSession())
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
            while (watch.ElapsedMilliseconds < 2000)
            {
                using (var session = DocumentStore.OpenReadSession())
                {
                    var projects = session.Query<Project>();
                    var project = session.Load<Project>("acme-13");
                    count += projects.Count;
                }
            }

            Trace.WriteLine("Loaded: " + count);
            Assert.That(count, Is.GreaterThan(50000));
        }

        [Test]
        public void ReadSession_CanReadHistoricalData()
        {
            using (var session = DocumentStore.OpenWriteSession())
            {
                session.Store(new Project { Id = "acme", Name = "ACME 1" });
                session.Commit("Added project");
            }

            string sha;
            using (var session = DocumentStore.OpenWriteSession())
            {
                sha = session.Anchor.Id;
                var proj = session.Load<Project>("acme");
                proj.Name = "ACME 2";
                session.Store(proj);
                session.Commit("Renamed project");
            }

            using (var session = DocumentStore.OpenReadSession())
            {
                var proj = session.Load<Project>("acme");
                Assert.That(proj.Name, Is.EqualTo("ACME 2"));
            }

            using (var session = DocumentStore.OpenReadSession(sha))
            {
                var proj = session.Load<Project>("acme");
                Assert.That(proj.Name, Is.EqualTo("ACME 1"));
            }
        }

        [Test]
        public void ReadSession_CanReadHistoricalAttachments()
        {
            using (var session = DocumentStore.OpenWriteSession())
            {
                session.Attachments.Store("readme.md", "Hello **world**!\r\n");
                session.Commit("Added readme");
            }

            using (var session = DocumentStore.OpenWriteSession())
            {
                var readme = session.Attachments.LoadText("readme.md");
                session.Attachments.Store("readme.md", readme.Replace("Hello", "Goodbye"));
                session.Commit("Updated readme");
            }

            using (var session = DocumentStore.OpenReadSession())
            {
                var readme = session.Attachments.LoadText("readme.md");
                Assert.That(readme, Is.EqualTo("Goodbye **world**!\r\n"));
            }

            using (var session = DocumentStore.OpenReadSession(DocumentStore.GetAnchors().Last()))
            {
                var readme = session.Attachments.LoadText("readme.md");
                Assert.That(readme, Is.EqualTo("Hello **world**!\r\n"));
            }
        }

        [Test]
        public void Store_CanListAnchors()
        {
            using (var session = DocumentStore.OpenWriteSession())
            {
                session.Store(new Project { Id = "acme-1", Name = "ACME 1" });
                session.Commit("Added project");
            }

            using (var session = DocumentStore.OpenWriteSession())
            {
                session.Store(new Project { Id = "acme-2", Name = "ACME 1" });
                session.Commit("Added another project");
            }

            using (var session = DocumentStore.OpenWriteSession())
            {
                session.Store(new Project { Id = "acme-3", Name = "ACME 1" });
                session.Commit("Added yet another project");
            }

            var anchors = DocumentStore.GetAnchors();
            Assert.That(anchors.Count, Is.EqualTo(3));
            Assert.That(anchors[0].Message, Is.EqualTo("Added yet another project"));
            Assert.That(anchors[1].Message, Is.EqualTo("Added another project"));
            Assert.That(anchors[2].Message, Is.EqualTo("Added project"));
        }
    }
}
