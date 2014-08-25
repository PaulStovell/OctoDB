using System.Collections.Generic;

namespace OctoDB.Tests.SampleModel
{
    [Document(@"projects\{id}\project.json")]
    public class Project
    {
        public string Id { get; set; }
        public string Name { get; set; }
        [Attached("readme.md")]
        public string Description { get; set; }

        [Attached(@"scriptModule.psm1")]
        public string ScriptModule { get; set; }
    }

    [Document(@"projects\{id}\process.json")]
    public class DeploymentProcess
    {
        public string Id { get; set; }
        public List<Step> Steps { get; set; }
    }

    [Document(@"projects\{id}\variables.json")]
    public class VariableSet
    {
        public VariableSet()
        {
            Variables = new Dictionary<string, string>();
        }

        public string Id { get; set; }
        public Dictionary<string, string> Variables { get; set; }
    }

    [Document(@"environments\{id}.json")]
    public class DeploymentEnvironment
    {
        public string Id { get; set; }
        public string Name { get; set; }
    }

    [Document(@"machines\{id}.json")]
    public class Machine
    {
        public Machine()
        {
            Properties = new Dictionary<string, string>();
        }

        public string Id { get; set; }
        public string Name { get; set; }
        public Dictionary<string, string> Properties { get; set; } 
    }

    [Document(@"library\script-modules\{id}\module.json")]
    public class ScriptModule
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }

        [Attached("module.psm1")]
        public string Module { get; set; }
    }
}
