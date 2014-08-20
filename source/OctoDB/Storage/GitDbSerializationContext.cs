using System;
using System.Collections.Generic;

namespace OctoDB.Storage
{
    public class GitDbSerializationContext
    {
        private readonly Func<object, string, ExternalAttribute, object> onRequestAttachment;
        readonly List<AttachmentFoundEvent> attachments = new List<AttachmentFoundEvent>();

        public GitDbSerializationContext(Func<object, string, ExternalAttribute, object> onRequestAttachment)
        {
            this.onRequestAttachment = onRequestAttachment;
        }

        public List<AttachmentFoundEvent> Attachments
        {
            get { return attachments; }
        }

        public object RequestAttachment(object owner, string propertyName, ExternalAttribute attribute)
        {
            return onRequestAttachment(owner, propertyName, attribute);
        }
    }
}