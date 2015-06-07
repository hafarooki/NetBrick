using Newtonsoft.Json;
using Newtonsoft.Json.Bson;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

public static partial class ExtensionMethods
{
    public static byte[] ToBytes(this object value)
    {
        using(var ms = new MemoryStream())
        {
            using(var writer = new BsonWriter(ms))
            {
                var serializer = new JsonSerializer();
                serializer.Serialize(writer, value);
                return ms.ToArray();
            }
        }
    }

    public static T FromBytes<T>(this byte[] value, bool array = false)
    {
        using(var ms = new MemoryStream(value))
        {
            using(var reader = new BsonReader(ms))
            {
                var serializer = new JsonSerializer();
                reader.ReadRootValueAsArray = array;
                return serializer.Deserialize<T>(reader);
            }
        }
    }
}
