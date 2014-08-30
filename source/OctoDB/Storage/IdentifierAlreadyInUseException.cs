using System;

namespace OctoDB.Storage
{
    public class IdentifierAlreadyInUseException : Exception
    {
        public IdentifierAlreadyInUseException(string message) : base(message)
        {
            
        }
    }
}