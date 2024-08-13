using System;
using System.Text.Json;

namespace textgen
{
    public static class ObjectExtensions
    {
        public static T DeepClone<T>(this T obj)
        {
            if (obj == null)
            {
                throw new ArgumentNullException(nameof(obj), "Object cannot be null");
            }

            var json = JsonSerializer.Serialize(obj);
            return JsonSerializer.Deserialize<T>(json);
        }
    }
}