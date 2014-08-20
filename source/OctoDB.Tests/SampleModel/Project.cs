using System.Collections.Generic;

namespace OctoDB.Tests.SampleModel
{
    public class Project
    {
        public string Id { get; private set; }
        public string Name { get; set; }
        public string Description { get; set; }

        [External("scriptModule.psm1")]
        public string ScriptModule { get; set; }

        public List<Step> Steps { get; set; }
    }
}
