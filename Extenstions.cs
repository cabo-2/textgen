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
            if (string.IsNullOrWhiteSpace(apiHost))
            {
                return ApiEndpoint.Unsupported;
            }

            if (apiHost.IsGeminiHost())
            {
                return ApiEndpoint.Gemini;
            }

            if (apiHost.EndsWith("/completion", StringComparison.OrdinalIgnoreCase))
            {
                return ApiEndpoint.Llama;
            }
            if (apiHost.Contains("/v1/chat/completions", StringComparison.OrdinalIgnoreCase)
                || apiHost.Contains("/v1/completions", StringComparison.OrdinalIgnoreCase))
            {
                return ApiEndpoint.OpenAi;
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

        public static bool IsGeminiHost(this string apiHost)
        {
            if (!Uri.TryCreate(apiHost, UriKind.Absolute, out var uri))
                return false;

            return uri.Host.EndsWith("generativelanguage.googleapis.com", StringComparison.OrdinalIgnoreCase);
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
