using System;

namespace OctoDB
{
    public class DocumentAttribute : Attribute
    {
        public DocumentAttribute(string path)
        {
            Path = path;
        }

        public string Path { get; set; }
    }
}