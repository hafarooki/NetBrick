using System.IO;
using Newtonsoft.Json;
using System.Runtime.Serialization.Formatters.Binary;
using System;

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
                //using (var writer = new BsonWriter(ms))
                //{
                //    var serializer = new JsonSerializer();
                //    serializer.Serialize(writer, value);
                //    return ms.ToArray();
                //}
                var bf = new BinaryFormatter();
                bf.Serialize(ms, value);
                return ms.ToArray();
            }
        }

        [Obsolete("No longer necessary to have array bool", false)]
        public static T FromBytes<T>(this byte[] value, bool array)
        {
            return FromBytes<T>(value);
        }

        public static T FromBytes<T>(this byte[] value)
        {
            using (var ms = new MemoryStream(value))
            {
                //using (var reader = new BsonReader(ms))
                //{
                //    var serializer = new JsonSerializer();
                //    return serializer.Deserialize<T>(reader);
                //}
                var bf = new BinaryFormatter();
                return (T)bf.Deserialize(ms);
            }
        }
    }
}