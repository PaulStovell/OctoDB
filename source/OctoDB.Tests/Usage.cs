using System.Collections.Generic;
using System.Diagnostics;
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
        public void ReadOnlySessionsReuseInstances()
        {
            using (var batch = Storage.Batch())
            {
                batch.Put(new Project { Id = "acme-1", Description = "A", ScriptModule = "write-host 'hi'\r\n"});
                batch.Commit("Added project 1");
            }

            Project projectA;
            using (var session = Store.OpenReadSession())
            {
                projectA = session.Load<Project>("acme-1");
            }

            Project projectB;
            using (var session = Store.OpenReadSession())
            {
                projectB = session.Load<Project>("acme-1");
            }

            Assert.AreEqual(projectA, projectB);
            Assert.That(projectB.Description, Is.EqualTo("A"));
            Assert.That(projectB.ScriptModule, Is.EqualTo("write-host 'hi'\r\n"));

            using (var session = Store.OpenWriteSession())
            {
                var project = session.Load<Project>("acme-1");
                project.Description = "B";
                session.Store(project);
                session.Commit("Changed project 1");
            }
            
            Project projectC;
            using (var session = Store.OpenReadSession())
            {
                projectC = session.Load<Project>("acme-1");
            }

            Assert.AreNotEqual(projectC, projectB);
            Assert.That(projectC.Description, Is.EqualTo("B"));
        }

        [Test]
        public void CanUseStore()
        {
            using (var batch = Storage.Batch())
            {
                for (var i = 0; i < 50; i++)
                {
                    var project = new Project
                    {
                        Id = "acme-" + i,
                        Name = "My project 2",
                        Description = "Foo",
                        ScriptModule = "Write-Host 'Hello'\r\n",
                        Steps = new List<Step> {new Step {Id = "StepABCDEFG", ScriptModule = "Hello"}}
                    };

                    batch.Put(project);
                }

                batch.Commit("Added project 1");
            }

            var store = new Store(Storage);

            var watch = Stopwatch.StartNew();
            var count = 0;
            while (watch.ElapsedMilliseconds < 5000)
            {
                using (var session = store.OpenReadSession())
                {
                    var projects = session.Query<Project>();
                    var project = session.Load<Project>("acme-13");
                    count += projects.Count;
                }
            }

            Trace.WriteLine("Loaded: " + count);
        }

        [Test]
        public void CanCreateAndLoadProject()
        {
            using (var batch = Storage.Batch())
            {
                for (var i = 0; i < 50; i++)
                {
                    var project = new Project
                    {
                        Id = "acme-" + i,
                        Name = "My project 2",
                        Description = "Foo",
                        ScriptModule = "Write-Host 'Hello'\r\n",
                        Steps = new List<Step> {new Step {Id = "StepABCDEFG", ScriptModule = "Hello"}}
                    };

                    batch.Put(project);
                }

                batch.Commit("Added project 1");
            }

            var watch = Stopwatch.StartNew();
            var count = 0;
            while (watch.ElapsedMilliseconds < 5000)
            {
                var reference = Storage.GetCurrentAnchor();
                //var snapshot = Storage.LoadSnapshot(reference);
                var projects = Storage.LoadAll<Project>(reference);

                count += projects.Count;

                var loaded = Storage.Load<Project>(reference, "acme-14");
                Assert.That(loaded.Name, Is.EqualTo("My project 2"));
                Assert.That(loaded.ScriptModule, Is.EqualTo("Write-Host 'Hello'\r\n"));
            }

            Trace.WriteLine("Loaded: " + count);
        }
    }
}
