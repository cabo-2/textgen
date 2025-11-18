#nullable enable
using System;

namespace Textgen
{
    public abstract class TextGenerator
    {
        protected readonly HttpClient _httpClient;
        protected readonly string _apiHost;
        protected readonly ILogger? _logger;

        public TextGenerator(HttpClient httpClient, string apiHost, ILogger? logger = null)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _apiHost = apiHost ?? throw new ArgumentNullException(nameof(apiHost));
            _logger = logger;
        }

        public abstract Task<OutputResult> GenerateTextAsync(
            string model,
            string prompt,
            string system,
            IConfig conf,
            OutputResult conversationLog,
            IReadOnlyList<Attachment>? promptAttachments = null,
            CancellationToken cancellationToken = default);

        public abstract IConfig CreateDefaultConfig();
    }
}
