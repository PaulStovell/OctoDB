OctoDB allows you to treat a Git repository as if it were a document store. Like a document store, it persists rich objects as JSON. 

### Usage

Given the following document type:

    [Document(@"projects\{id}\project.json")]
    public class Project
    {
        public string Id { get; set; }
        public string Name { get; set; }
        
        [Attached("readme.md")]
        public string Description { get; set; }
    }

We can persist a document like this:

    var store = new Store("C:\Repo");
    using (var session = store.OpenWriteSession())
    {
        session.Store(new Project { Id = "acme", Name = "ACME", Description = "Best project **ever**!" });
        session.Commit("Added another project");
    }

On disk, we would have a Git repository with one commit, that adds:

    projects\acme\project.json
    projects\acme\readme.md
    
### Design decisions

OctoDB isn't meant for everyone. Like any storage solution, OctoDB makes a number of trade-offs. OctoDB is intended for data sets that: 

 - Are read more often than they are written
 - Can fit entirely in memory
 - Benefit from the automatic history tracking and distributed nature of Git
 - Are only being used by one application

Obviously, this means that OctoDB isn't a good solution for many scenarios. You wouldn't process transactions with it, or store customers for an e-commerce solution in it. 

You might, however, use it to store configuration data for a build server, or configuration settings for a financial model. OctoDB is a great choice when you'd otherwise just keep all the data you need in memory forever, except for the annoying need to persist and audit changes. 

If you've ever thought "Git would be a great store for this kind of information" but not wanted to litter your code with file system references, then OctoDB is a good wrapper. 

### Sessions

The key unit of work in OctoDB is a session, and there are two kinds of sessions. Which one you use depends on what you plan to do during the session. 

    using (var session = store.OpenReadSession()) { ... }
    using (var session = store.OpenWriteSession()) { ... }

As suggested, read sessions can only be used to read data. Write sessions can be used to both read and write. In general, if you are building a HTTP application, a GET request would only need a read session; PUT, POST or DELETE requests would use a write session.

#### Write sessions

To store information in OctoDB, a write session is used (write sessions can also read data, but should only be used for reading smaller subsets of data). 

    using (var session = store.OpenWriteSession()) 
    {
        session.Store(new Project { Id = "acme-web", Name = "ACME Web" });
        session.Store(new Project { Id = "acme-service", Name = "ACME Service" });
        
        var oldProject = session.Load<Project>("acme-legacy");
        session.Delete(oldProject);
        
        session.Commit("Added ACME projects and removed legacy project");
    }

A write session is a unit of work that creates a list of pending store (create/update) or delete operations, that are then commited to the Git repository as a new commit. 

Write sessions support the following operations:

    Query<T> - load and return a List<T> of all documents of a given type
    Load<T>(id) - load and return a single document by ID
    Load<T>(ids) - return a List<T> of documents by their ID's
    Store(doc) - the new document will be stored when the session is committed
    Delete(doc) - the new document will be stored when the session is committed

As you would expect, sesions use an identity map to ensure a given object by ID is only ever loaded once. 

#### Commit anchoring

All sessions in OctoDB are "anchored" to a Git commit. When you open a session, the SHA of the current Git commit is captured. This SHA is then used as a reference when reading any data out of the store for the lifetime of the session. 

**This means that within a session, you are guaranteed to be reading against a consistent snapshot of data.**

#### Read sessions

Since OctoDB assumes it works on small data sets that can fit in memory and don't change too frequently, OctoDB has explicit support for **read-only** sessions. 

    using (var session = store.OpenReadSession()) 
    {
        var project = session.Load<Project>("acme");
        var projects = session.Query<Project>();
    }

The special thing about read sessions is that they take advantage of caching of the data. 

 1. When the first read session is opened, the entire data set is loaded into memory
 2. When the next read session is opened, only **changes** to the data set need to be loaded

This means that between read sessions, if nothing has changed, there's zero I/O, zero deserialization, and zero object creation or garbage collection. The same object references will be reused: 

    Project a;
    using (var session = store.OpenReadSession()) 
    {
        a = session.Load<Project>("acme");
    }
    
    Project b;
    using (var session = store.OpenReadSession()) 
    {
        b = session.Load<Project>("acme");
    }
    
    Assert.AreEqual(a, b);

Since the entire data set is in memory, the `Query<T>` method on sessions just returns a `List<T>`. You can use LINQ to Objects to then query this list. 

If there have been changes between the two read sessions being opened, then OctoDB will compare the Git trees of the two commits anchored to the sessions, and only load the differences. 

Read sessions only support the following operations:

    Query<T> - return a List<T> of all documents of a given type
    Load<T>(id) - return a single document by ID
    Load<T>(ids) - return a List<T> of documents by their ID's

Again, since all data is loaded up-front in a read session, these methods are only operating against an in-memory dictionary. 

### Attachments

By default, properties on documents are persisted using JSON. The `[Document]` attribute determines the path to the document file on disk. 

For some properties, it might make sense to store the property value as an external file separate to the JSON file. For this, the `[Attachment]` attribute can be used on the property: 

    [Document(@"projects\{id}\project.json")]
    public class Project
    {
        public string Id { get; set; }
        public string Name { get; set; }
        
        [Attached("readme.md")]
        public string Description { get; set; }
        
        [Attached("logo.png")]
        public byte[] LogoImage { get; set; }
    }

This allows you to ensure that the Git repository looks clean and that documents can be stored using the most appropriate formats for diffing. 

### Concurrency

At any point in time, you can have multiple read and write sessions open. The `Store` can be used to open sessions on multiple threads, though individual sessions are intended to be used from a single thread. 

Internally, OctoDB uses a reader-writer lock. Reading the repository uses the read lock. When calling `Commit` on a write sesion, a write lock is taken, which will pause further reads. At this point:

1. All pending store and delete operations are written to disk
2. Changes are staged in git (this is the slowest part)
3. A commit is made
4. The write lock is released

Since read sessions use in-memory snapshots, assuming you only perform one commit per second, you should be able to perform millions of read operations between thos commits. And assuming the commits only touch a few dozen documents at once, updating the in-memory snapshot is also very quick. 

As a guide, committing 3000 documents to the store takes about 30 seconds on a rMBP, most of which is in the git staging command. Loading the entire data set takes about 10 seconds. Writing a single document takes 800ms, while comparing the trees and updating the in-memory read snapshot takes 55ms. 

### ID's and paths

There is currently no ID generation strategy in OctoDB - no auto-numbers, no identity fields, no HiLo; we assume ID's are assigned manually. 

ID's are only important in OctoDB in that they are mapped to paths. Different documents can use similar paths, but a given path needs to resolve to only a single document type. For example:

   [Document("projects\{id}\project.json")] class Project ...
   [Document("projects\{id}\settings.json")] class ProjectSettings ...
   [Document("projects\{id}\team.json")] class ProjectTeam ...
   [Document("machines\{id}.json")] class Machine ...

For a given document type, the same ID can be used - for example, "acme" could be used as the ID of both a project and a machine in the example above, but two projects named "acme" would not be possible. 

## Implementation

Internally, OctoDB uses [libgit2sharp](https://github.com/libgit2/libgit2sharp) to interact with the Git repository, and JSON.NET to serialize documents. 

When reading/querying, OctoDB reads from Git trees and blobs directly, allowing different sessions to use different commits as a reference point. When committing a write session, the file system is used. Since there is only one write operation active at any one time (due to the write lock) there is no conflict. 

OctoDB always commits to the `master` branch of the Git repository. 
