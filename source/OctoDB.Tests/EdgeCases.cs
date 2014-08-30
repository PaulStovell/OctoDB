using System;
using NUnit.Framework;
using OctoDB.Storage;
using OctoDB.Tests.Fixtures;
using OctoDB.Tests.SampleModel;

namespace OctoDB.Tests
{
    [TestFixture]
    public class EdgeCases : StorageFixture
    {
        [Test]
        public void WriteSession_CannotStoreTwoObjectsWithSameId()
        {
            using (var session = Store.OpenWriteSession())
            {
                session.Store(new Project { Id = "acme", Name = "ACME Web 1" });
                Assert.Throws<IdentifierAlreadyInUseException>(() => session.Store(new Project {Id = "acme", Name = "ACME Web 2"}));
            }
        }

        [Test]
        public void WriteSession_CanStoreTwoObjectsWithSameIdButDifferentType()
        {
            using (var session = Store.OpenWriteSession())
            {
                session.Store(new Project { Id = "acme", Name = "ACME Web 1" });
                session.Store(new VariableSet {Id = "acme"});
                session.Commit("Added a project and variable set");
            }
        }

        [Test]
        public void WriteSession_StoreNullIsIgnored()
        {
            using (var session = Store.OpenWriteSession())
            {
                session.Store(null);
                session.Commit("Did nothing");
            }
        }

        [Test]
        public void WriteSession_DeleteUnstoredObjectIsIgnored()
        {
            using (var session = Store.OpenWriteSession())
            {
                session.Delete(new Project { Id = "acme" });
                session.Commit("Did nothing");
            }
        }

        [Test]
        public void WriteSession_DeleteNullIsIgnored()
        {
            using (var session = Store.OpenWriteSession())
            {
                session.Store(null);
                session.Commit("Did nothing");
            }
        }

        [Test]
        public void WriteSession_CannotStoreDocumentsWithoutAttribute()
        {
            using (var session = Store.OpenWriteSession())
            {
                Assert.Throws<InvalidOperationException>(() => session.Store(new TypeWithNoAttribute()));
                Assert.Throws<InvalidOperationException>(() => session.Store(new TypeWithNoId()));
                Assert.Throws<FormatException>(() => session.Store(new TypeWithNoIdInPath()));
            }
        }

        class TypeWithNoAttribute {  }

        [Document("foo\\{id}.json")]
        class TypeWithNoId {  }

        [Document("foo.json")]
        class TypeWithNoIdInPath { }
    }
}