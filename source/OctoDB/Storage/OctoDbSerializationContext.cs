using System;

namespace OctoDB.Storage
{
    public class OctoDbSerializationContext
    {
        private readonly Func<object[], object, string, AttachedAttribute, object> onRequestAttachment;
        private readonly Action<object[], object, string, object, AttachedAttribute> onNotifyAttachment;

        public OctoDbSerializationContext(Func<object[], object, string, AttachedAttribute, object> onRequestAttachment, Action<object[], object, string, object, AttachedAttribute> onNotifyAttachment)
        {
            this.onRequestAttachment = onRequestAttachment;
            this.onNotifyAttachment = onNotifyAttachment;
        }

        public object RequestAttachmentValue(object[] deserializationStack, object owner, string propertyName, AttachedAttribute attribute)
        {
            return onRequestAttachment(deserializationStack, owner, propertyName, attribute);
        }

        public void NotifyAttachment(object[] serializationStack, object owner, string propertyName, object value, AttachedAttribute attribute)
        {
            onNotifyAttachment(serializationStack, owner, propertyName, value, attribute);
        }
    }
}