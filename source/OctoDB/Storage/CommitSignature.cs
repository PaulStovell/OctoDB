using System;

namespace OctoDB.Storage
{
    public class CommitSignature
    {
        readonly string name;
        readonly string emailAddress;
        readonly DateTimeOffset when;

        public CommitSignature(string name, string emailAddress, DateTimeOffset when)
        {
            this.name = name;
            this.emailAddress = emailAddress;
            this.when = when;
        }

        public override string ToString()
        {
            return name;
        }
    }
}