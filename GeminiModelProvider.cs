using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace textgen
{
    public class GeminiModelProvider : IModelProvider
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiHost;
        private readonly string _apiKey;

        public GeminiModelProvider(HttpClient httpClient, string apiHost, string apiKey)
        {
            _httpClient = httpClient;
            _apiHost = apiHost;
            _apiKey = apiKey;
        }

        public async Task<List<string>> GetModelsAsync()
        {
            var requestUrl = $"{_apiHost}?key={_apiKey}";

            try
            {
                HttpResponseMessage response = await _httpClient.GetAsync(requestUrl);
                response.EnsureSuccessStatusCode();
                string responseContent = await response.Content.ReadAsStringAsync();

                dynamic json = JsonConvert.DeserializeObject(responseContent);
                var models = new List<string>();
                if (json.models != null)
                {
                    foreach (var model in json.models)
                    {
                        // Assuming the model name is in a property named 'name'
                        string name = model.name.ToString();
                        if (name.StartsWith("models/"))
                        {
                            name = name.Substring("models/".Length);
                        }
                        if (!string.IsNullOrEmpty(name))
                        {
                            models.Add(name);
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
    }
}
