using System.IO;
using System.Text;

namespace OctoDB.Storage
{
    public static class StorageBatchExtensions
    {
        public static void PutText(this IStorageBatch batch, string path, string text, Encoding encoding = null)
        {
            encoding = encoding ?? Encoding.UTF8;
            batch.Put(path, stream =>
            {
                using (var writer = new StreamWriter(stream, encoding))
                {
                    writer.Write(text);
                }
            });
        }
    }
}