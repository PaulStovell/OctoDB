using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace OctoDB.Storage
{
    public class EncoderSelector : Collection<IEncoder>, IEncoderRegistry
    {
        public object Decode(string path, Stream input)
        {
            var encoder = this.First(e => e.CanDecode(path));
            return encoder.Decode(path, input);
        }

        public void Encode(string path, object document, Stream output)
        {
            var type = document.GetType();
            var encoder = this.First(e => e.CanEncode(type));
            encoder.Encode(path, document, output);
        }
    }
}