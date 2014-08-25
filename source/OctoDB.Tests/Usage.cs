using System;
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
        public void CreateSampleDataSet()
        {
            var createWatch = Stopwatch.StartNew();
            using (var session = Store.OpenWriteSession())
            {
                for (var i = 0; i < 300; i++)
                {
                    session.Store(new Project { Id = "acme-" + i, Name = "ACME " + i, Description = "My **best** project" });
                    session.Store(new DeploymentProcess { Id = "acme-" + i, Steps = new List<Step>
                    {
                        new Step { Name = "Step 1", Id = Guid.NewGuid().ToString(), Properties =
                        {
                            { "Foo.Bar", "Hello" },
                            { "Foo.Baz", "Bye!" },
                        }}
                    } });
                    session.Store(new VariableSet { Id = "acme-" + i, Variables =
                    {
                        { "DatabaseName", "MyDB" },
                        { "ConnectionString", "Server=(local);Database=#{DatabaseName};trusted_Connection=true" },
                    } });
                }

                for (var i = 0; i < 100; i++)
                {
                    session.Store(new DeploymentEnvironment { Id = "env-" + i, Name = "Environment " + i });
                }

                for (var i = 0; i < 2000; i++)
                {
                    session.Store(new Machine { Id = "machine-" + i, Name = "Machine " + i, Properties =
                    {
                        { "Url", "https://localhost:8080/" }
                    }});
                }

                session.Commit("Create initial data set");
            }

            Console.WriteLine("Create took: " + createWatch.ElapsedMilliseconds + "ms");

            createWatch.Restart();

            using (var session = Store.OpenReadSession())
            {
                
            }

            Console.WriteLine("Read: " + createWatch.ElapsedMilliseconds + "ms");

            createWatch.Restart();

            using (var session = Store.OpenReadSession())
            {

            }

            Console.WriteLine("Read again: " + createWatch.ElapsedMilliseconds + "ms");

            createWatch.Restart();

            using (var session = Store.OpenWriteSession())
            {
                session.Store(new Project { Id = "bam" });
                session.Commit("Added another project");
            }

            Console.WriteLine("Write one document: " + createWatch.ElapsedMilliseconds + "ms");

            createWatch.Restart();

            using (var session = Store.OpenReadSession())
            {

            }

            Console.WriteLine("Read again: " + createWatch.ElapsedMilliseconds + "ms");
        }

        [Test]
        public void NoChanges()
        {
            using (var session = Store.OpenWriteSession())
            {
                session.Store(new Project { Id = "bam" });
                session.Commit("Added another project");
            }

            using (var session = Store.OpenWriteSession())
            {
                session.Store(new Project { Id = "bam" });
                session.Commit("Added another project");
            }
        }

        [Test]
        public void ReadOnlySessionsReuseInstances()
        {
            if (Store.StorageEngine.IsRepositoryEmpty)
            {
                using (var batch = Store.StorageEngine.Batch())
                {
                    //batch.PutText(".gitattributes", "* text=auto");
                    batch.PutText("readme.md", "Hello **world**!");
                    batch.Commit("Initialize empty repository");
                }
            }

            using (var session = Store.OpenWriteSession())
            {
                session.Store(new Project { Id = "acme-1", Description = "A", ScriptModule = "write-host 'hi'\r\n" });
                session.Commit("Added project 1");
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
            using (var session = Store.OpenWriteSession())
            {
                for (var i = 0; i < 50; i++)
                {
                    var project = new Project
                    {
                        Id = "acme-" + i,
                        Name = "My project 2",
                        Description = "Foo",
                        ScriptModule = "Write-Host 'Hello'\r\n"
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
    }
}
