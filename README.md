OctoDB is an opinionated document store built on top of Git. 

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
    

## Read sessions



## Write sessions

## Attachments



A document store built on top of Git
