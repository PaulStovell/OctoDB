namespace OctoDB.Tests.SampleModel
{
    public class Step
    {
        public string Id { get; set; }

        [External("scriptModule.psm1")]
        public string ScriptModule { get; set; }
    }
}