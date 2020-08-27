using Newtonsoft.Json;
using System.IO;

namespace DotmimSyncLegacy.Serialization
{
    public class JsonConverter<T> : BaseConverter<T>
    {
        public override T Deserialize(Stream ms)
        {
            using (StreamReader sr = new StreamReader(ms))
            using (JsonReader reader = new JsonTextReader(sr))
            {
                JsonSerializer serializer = new JsonSerializer();
                return serializer.Deserialize<T>(reader);
            }
        }

        public override void Serialize(T obj, Stream ms)
        {
            using (var writer = new StreamWriter(ms))
            using (var jsonWriter = new JsonTextWriter(writer))
            {
                var serializer = new JsonSerializer();
                serializer.Serialize(jsonWriter, obj);
            }
        }

        public override byte[] Serialize(T obj)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                Serialize(obj, ms);
                return ms.ToArray();
            }
        }
    }
}
