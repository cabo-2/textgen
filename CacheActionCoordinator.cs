using System.Text;
using System.Text.Json;

namespace Textgen
{
    public record CacheActionOptions(string RestoreFile, string SaveFile)
    {
        public bool HasRestore => !string.IsNullOrWhiteSpace(RestoreFile);
        public bool HasSave => !string.IsNullOrWhiteSpace(SaveFile);
        public bool HasAny => HasRestore || HasSave;
    }

    public interface ICacheActionCoordinator
    {
        Task BeforeChatAsync(CancellationToken cancellationToken);
        Task AfterChatAsync(CancellationToken cancellationToken);
    }

    public static class CacheActionCoordinatorFactory
    {
        public static ICacheActionCoordinator Create(ApiEndpoint endpoint, string apiHost, HttpClient httpClient, CacheActionOptions options, ILogger logger)
        {
            if (endpoint == ApiEndpoint.Llama && options.HasAny)
            {
                return new LlamaServerCacheCoordinator(apiHost, httpClient, options, logger);
            }

            return new NoOpCacheCoordinator();
        }
    }

    internal sealed class NoOpCacheCoordinator : ICacheActionCoordinator
    {
        public Task BeforeChatAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task AfterChatAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    internal sealed class LlamaServerCacheCoordinator : ICacheActionCoordinator
    {
        private readonly string _apiHost;
        private readonly HttpClient _httpClient;
        private readonly CacheActionOptions _options;
        private readonly ILogger _logger;

        public LlamaServerCacheCoordinator(string apiHost, HttpClient httpClient, CacheActionOptions options, ILogger logger)
        {
            _apiHost = apiHost ?? throw new ArgumentNullException(nameof(apiHost));
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger;
        }

        public async Task BeforeChatAsync(CancellationToken cancellationToken)
        {
            if (!_options.HasRestore)
                return;

            await SendActionAsync("restore", _options.RestoreFile, cancellationToken).ConfigureAwait(false);
        }

        public async Task AfterChatAsync(CancellationToken cancellationToken)
        {
            if (!_options.HasSave)
                return;

            await SendActionAsync("save", _options.SaveFile, cancellationToken).ConfigureAwait(false);
        }

        private async Task SendActionAsync(string action, string filename, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(filename))
                throw new ArgumentException("Filename must be specified for cache actions.", nameof(filename));

            var requestUrl = BuildCacheActionUrl(action);
            var payload = JsonSerializer.Serialize(new { filename });
            using var content = new StringContent(payload, Encoding.UTF8, "application/json");

            _logger?.Log($"Cache {action} request -> {requestUrl} with file '{filename}'");

            using var response = await _httpClient.PostAsync(requestUrl, content, cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                throw new InvalidOperationException($"Cache {action} failed with status {(int)response.StatusCode}: {errorBody}");
            }
        }

        private string BuildCacheActionUrl(string action)
        {
            if (!Uri.TryCreate(_apiHost, UriKind.Absolute, out var apiUri))
                throw new InvalidOperationException("TEXTGEN_API_HOST must be an absolute URI to use cache actions.");

            var builder = new UriBuilder(apiUri)
            {
                Path = "slots/0",
                Query = $"action={action}"
            };

            return builder.Uri.ToString();
        }
    }
}
