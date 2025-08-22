using Newtonsoft.Json.Linq;

namespace Textgen
{
    public class OpenAIModelProvider : IModelProvider
    {
        private readonly string _apiHost;
        private readonly HttpClient _httpClient;

        public OpenAIModelProvider(HttpClient httpClient, string apiHost)
        {
            _httpClient = httpClient;
            _apiHost = apiHost;
        }

        public async Task<List<string>> GetModelsAsync()
        {
            var models = new List<string>();
            string baseUrl = GetBaseUrl(_apiHost);
            try
            {
                if (baseUrl == null)
                    throw new InvalidOperationException("Invalid API host URL");

                string modelsUrl = $"{baseUrl}/v1/models";

                HttpResponseMessage response = await _httpClient.GetAsync(modelsUrl);
                response.EnsureSuccessStatusCode();
                string responseContent = await response.Content.ReadAsStringAsync();

                JObject json = JObject.Parse(responseContent);
                var data = json["data"];
                if (data != null && data.Type == JTokenType.Array)
                {
                    foreach (var model in data)
                    {
                        string id = model["id"]?.ToString();
                        if (!string.IsNullOrEmpty(id))
                        {
                            models.Add(id);
                        }
                    }
                }

                return models;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error querying models: {ex.Message}");
                return null;
            }
        }

        private string GetBaseUrl(string apiHost)
        {
            try
            {
                Uri uri = new Uri(apiHost);
                string baseUrl = $"{uri.Scheme}://{uri.Host}";

                if (!uri.IsDefaultPort)
                {
                    baseUrl += $":{uri.Port}";
                }

                return baseUrl;
            }
            catch
            {
                return null;
            }
        }
    }
}
