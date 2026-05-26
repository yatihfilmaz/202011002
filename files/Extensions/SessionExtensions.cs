using System.Text.Json;

namespace project.Extensions // Kendi projenin namespace'ine göre uyarla
{
    public static class SessionExtensions
    {
        // Sepeti JSON formatına çevirip Session'a kaydeder
        public static void SetObjectAsJson(this ISession session, string key, object value)
        {
            session.SetString(key, JsonSerializer.Serialize(value));
        }

        // Session'daki JSON verisini okuyup tekrar listeye (C# nesnesine) çevirir
        public static T? GetObjectFromJson<T>(this ISession session, string key)
        {
            var value = session.GetString(key);
            return value == null ? default(T) : JsonSerializer.Deserialize<T>(value);
        }
    }
}