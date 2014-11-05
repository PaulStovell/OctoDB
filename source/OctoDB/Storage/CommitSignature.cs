using System;

namespace OctoDB.Storage
{
    public class CommitSignature
    {
        public string Name { get; private set; }
        public string EmailAddress { get; private set; }
        public DateTimeOffset When { get; private set; }

        public CommitSignature(string name, string emailAddress, DateTimeOffset when)
        {
            Name = name;
            EmailAddress = emailAddress;
            When = when;
        }

        

        public override string ToString()
        {
            return Name;
        }
    }
}