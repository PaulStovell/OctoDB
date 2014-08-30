using NUnit.Framework;
using OctoDB.Storage;
using OctoDB.Tests.SampleModel;

namespace OctoDB.Tests
{
    [TestFixture]
    public class IdentityPathsFixture
    {
        [Test]
        public void ShouldMatchProject()
        {
            Conventions.Register(typeof(Project));

            var type = Conventions.GetType("projects\\acme\\project.json");
            Assert.That(type, Is.EqualTo(typeof(Project)));

            var proj = new Project();
            Conventions.AssignId("projects\\acme\\project.json", proj);
            Assert.That(proj.Id, Is.EqualTo("acme"));

            var path = Conventions.GetPath(typeof (Project), proj);
            Assert.That(path, Is.EqualTo("projects\\acme\\project.json"));
        }
    }
}