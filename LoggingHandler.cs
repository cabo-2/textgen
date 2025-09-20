using System.Text;

namespace Textgen
{
    /// <summary>
    /// DelegatingHandler that mirrors request / response data to an ILogger.
    /// </summary>
    public sealed class LoggingHandler : DelegatingHandler
    {
        private readonly ILogger _logger;

        public LoggingHandler(HttpMessageHandler inner, ILogger logger) : base(inner)
        {
            _logger = logger;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            _logger.Log($"--> {request.Method} {request.RequestUri}");
            if (request.Content != null &&
                request.Content.Headers.ContentType?.MediaType != "text/event-stream")
            {
                var reqBody = await request.Content.ReadAsStringAsync(cancellationToken);
                _logger.Log($"--> BODY {reqBody}");
            }

            var resp = await base.SendAsync(request, cancellationToken);

            _logger.Log($"<-- {(int)resp.StatusCode} {resp.ReasonPhrase}");
            if (resp.Content != null &&
                resp.Content.Headers.ContentType?.MediaType != "text/event-stream")
            {
                var originalHeaders = resp.Content.Headers
                    .Select(h => new KeyValuePair<string, IEnumerable<string>>(h.Key, h.Value.ToArray()))
                    .ToList();

                var bytes = await resp.Content.ReadAsByteArrayAsync(cancellationToken);
                var charset = resp.Content.Headers.ContentType?.CharSet;
                var bodyText = DecodeBody(bytes, charset);
                _logger.Log($"<-- BODY {bodyText}");

                var bufferedContent = new ByteArrayContent(bytes);
                foreach (var header in originalHeaders)
                {
                    if (string.Equals(header.Key, "Content-Length", StringComparison.OrdinalIgnoreCase))
                        continue; // ByteArrayContent manages its own length

                    bufferedContent.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }

                resp.Content = bufferedContent;
            }
            return resp;
        }

        private static string DecodeBody(byte[] bytes, string charset)
        {
            if (bytes == null || bytes.Length == 0)
                return string.Empty;

            try
            {
                if (!string.IsNullOrWhiteSpace(charset))
                {
                    return Encoding.GetEncoding(charset).GetString(bytes);
                }
            }
            catch (ArgumentException)
            {
                // Fall back to UTF-8 below when charset is unknown to the runtime
            }

            return Encoding.UTF8.GetString(bytes);
        }
    }
}
