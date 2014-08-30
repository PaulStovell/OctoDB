using System.Text;

namespace OctoDB.Storage
{
    public interface IReadAttachments
    {
        byte[] LoadBinary(string path);
        string LoadText(string path, Encoding encoding = null);
    }
}