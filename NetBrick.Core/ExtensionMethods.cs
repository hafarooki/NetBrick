using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Bson;

namespace NetBrick.Core
{
    public static class ExtensionMethods
    {
        public static string ToJson(this object value)
        {
            return JsonConvert.SerializeObject(value);
        }

        public static T FromJson<T>(this string value)
        {
            return JsonConvert.DeserializeObject<T>(value);
        }

        public static byte[] ToBytes(this object value)
        {
            using (var ms = new MemoryStream())
            {
                using (var writer = new BsonWriter(ms))
                {
                    var serializer = new JsonSerializer();
                    serializer.Serialize(writer, value);
                    return ms.ToArray();
                }
            }
        }

        public static T FromBytes<T>(this byte[] value, bool array = false)
        {
            using (var ms = new MemoryStream(value))
            {
                using (var reader = new BsonReader(ms))
                {
                    var serializer = new JsonSerializer();
                    reader.ReadRootValueAsArray = array;
                    return serializer.Deserialize<T>(reader);
                }
            }
        }
    }
}