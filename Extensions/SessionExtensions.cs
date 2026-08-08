using Microsoft.AspNetCore.Http;
using System.Text.Json;

namespace KaijensonIventory_SalesMotorShopWeb.Extensions
{
    public static class SessionExtensions
    {
        public static void SetObject(this ISession session, string key, object value)
        {
            session.SetString(key, JsonSerializer.Serialize(value));
        }

        public static T? GetObject<T>(this ISession session, string key) where T : class
        {
            var value = session.GetString(key);
            return value == null ? null : JsonSerializer.Deserialize<T>(value);
        }
    }
}
