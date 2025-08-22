namespace Textgen
{
    public abstract class TextGenerator
    {
        protected readonly HttpClient _httpClient;
        protected readonly string _apiHost;
        protected readonly ILogger _logger;

        public TextGenerator(HttpClient httpClient, string apiHost, ILogger logger = null)
        {
            _httpClient = httpClient;
            _apiHost = apiHost;
            _logger = logger;
        }

        public abstract Task<OutputResult> GenerateTextAsync(string model, string prompt, string system, IConfig conf, OutputResult conversationLog, CancellationToken cancellationToken = default);

        public abstract IConfig CreateDefaultConfig();
    }
}