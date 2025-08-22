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
                var body = await resp.Content.ReadAsStringAsync(cancellationToken);
                _logger.Log($"<-- BODY {body}");

                // replace consumed content so caller can still read
                resp.Content = new StringContent(body, Encoding.UTF8,
                    resp.Content.Headers.ContentType?.MediaType);
            }
            return resp;
        }
    }
}
