using System.IO;

namespace OctoDB.Storage
{
    public class NonClosableStream : Stream
    {
        readonly Stream proxy;

        public NonClosableStream(Stream proxy)
        {
            this.proxy = proxy;
        }

        public override void Flush()
        {
            proxy.Flush();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return proxy.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            proxy.SetLength(value);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return proxy.Read(buffer, offset, count);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            proxy.Write(buffer, offset, count);
        }

        public override bool CanRead
        {
            get { return proxy.CanRead; }
        }

        public override bool CanSeek
        {
            get { return proxy.CanSeek; }
        }

        public override bool CanWrite
        {
            get { return proxy.CanWrite; }
        }

        public override long Length
        {
            get { return proxy.Length; }
        }

        public override long Position
        {
            get { return proxy.Position; }
            set { proxy.Position = value; }
        }
    }
}