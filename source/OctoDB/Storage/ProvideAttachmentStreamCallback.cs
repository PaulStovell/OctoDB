using System;
using System.IO;

namespace OctoDB.Storage
{
    public delegate void ProvideAttachmentStreamCallback(string attachmentKey, Action<Stream> result);
}