using System.Collections.Generic;
using NUnit.Framework;
using OctoDB.Tests.SampleModel;

namespace OctoDB.Tests
{
    [TestFixture]
    public class Usage : StorageFixture
    {
        [Test]
        public void CanCreateAndLoadProject()
        {
            var project = new Project
            {
                Name = "My project 2",
                Description = "Foo",
                ScriptModule = "Write-Host 'Hello'\r\n",
                Steps = new List<Step> {new Step {Id = "StepABCDEFG", ScriptModule = "Hello"}}
            };

            using (var batch = Storage.Batch())
            {
                batch.Put("projects/project-1", project);
                batch.Commit("Added project 1");
            }

            var loaded = Storage.Load<Project>("projects/project-1");
            Assert.That(loaded.Name, Is.EqualTo("My project 2"));
            Assert.That(loaded.ScriptModule, Is.EqualTo("Write-Host 'Hello'\r\n"));
        }
    }
}
