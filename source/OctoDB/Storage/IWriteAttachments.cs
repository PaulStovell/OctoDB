using System.Text;

namespace OctoDB.Storage
{
    public interface IWriteAttachments : IReadAttachments
    {
        void Store(string path, byte[] contents);
        void Store(string path, string contents, Encoding encoding = null);
        void Delete(string path);
    }
}