using System.Collections.Generic;

namespace OctoDB.Tests.SampleModel
{
    public class Step
    {
        public Step()
        {
            Properties = new Dictionary<string, string>();
        }

        public string Id { get; set; }
        public string Name { get; set; }
        public Dictionary<string, string> Properties { get; set; }
    }
}