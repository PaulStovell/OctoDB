namespace OctoDB.Storage
{
    public class AttachmentFoundEvent
    {
        public object Owner { get; set; }
        public string PropertyName { get; set; }
        public object Value { get; set; }
        public AttachedAttribute Attribute { get; set; }
    }
}