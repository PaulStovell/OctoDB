using System;

namespace OctoDB
{
    public class ExternalAttribute : Attribute
    {
        private readonly string name;

        public ExternalAttribute(string name)
        {
            this.name = name;
        }

        public string Name { get { return name; } }
    }
}