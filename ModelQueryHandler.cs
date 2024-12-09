using System;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace textgen
{
    public class ModelQueryHandler
    {
        private readonly string _apiHost;
        private readonly HttpClient _httpClient;

        public ModelQueryHandler(string apiHost, HttpClient httpClient)
        {
            _apiHost = apiHost;
            _httpClient = httpClient;
        }

        public async Task<int> ExecuteAsync()
        {
            if (_apiHost.EndsWith("/v1/chat/completions"))
            {
                string baseUrl = GetBaseUrl(_apiHost);
                if (baseUrl == null)
                {
                    Console.Error.WriteLine("Error: Invalid API host URL.");
                    return 1;
                }

                string modelsUrl = $"{baseUrl}/v1/models";

                try
                {
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
                                Console.WriteLine(id);
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine("No models found.");
                    }
                    return 0;
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Error querying models: {ex.Message}");
                    return 1;
                }
            }
            else
            {
                Console.WriteLine("Unsupported endpoint.");
                return 1;
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
