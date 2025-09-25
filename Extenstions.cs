using System.Text.Json;

namespace Textgen
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

    public static class StringExtensions
    {
        public static ApiEndpoint GetApiEndpoint(this string apiHost)
        {
            if (apiHost.EndsWith("/completion"))
            {
                return ApiEndpoint.Llama;
            }
            if (apiHost.EndsWith("/v1/chat/completions"))
            {
                return ApiEndpoint.OpenAi;
            }
            if (apiHost == "https://generativelanguage.googleapis.com/v1beta/models")
            {
                return ApiEndpoint.Gemini;
            }
            return ApiEndpoint.Unsupported;
        }

        public static bool IsOfficialOpenAiHost(this string apiHost)
        {
            if (!Uri.TryCreate(apiHost, UriKind.Absolute, out var uri))
                return false;

            var authority = uri.GetLeftPart(UriPartial.Authority);
            if (string.IsNullOrEmpty(authority))
                return false;

            if (!authority.EndsWith("/", StringComparison.Ordinal))
                authority += "/";

            return string.Equals(authority, "https://api.openai.com/", StringComparison.OrdinalIgnoreCase);
        }
    }

    public enum ApiEndpoint
    {
        Llama,
        OpenAi,
        Gemini,
        Unsupported
    }
}
