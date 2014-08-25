using System;

namespace OctoDB
{
    public class AttachedAttribute : Attribute
    {
        private readonly string nameFormat;

        public AttachedAttribute(string nameFormat)
        {
            this.nameFormat = nameFormat;
        }

        public string NameFormat
        {
            get { return nameFormat; }
        }
    }
}