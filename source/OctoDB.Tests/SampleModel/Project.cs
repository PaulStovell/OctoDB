using System.Collections.Generic;

namespace OctoDB.Tests.SampleModel
{
    [Document(@"projects\{id}\project.json")]
    public class Project
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }

        [External("scriptModule.psm1")]
        public string ScriptModule { get; set; }

        public List<Step> Steps { get; set; }
    }
}
