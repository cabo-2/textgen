using System;

namespace textgen
{
    public static class TextGeneratorFactory
    {
        public static TextGenerator CreateGenerator(string apiHost, HttpClient httpClient, string apiKey = null, ILogger logger = null)
        {
            ApiEndpoint endpoint = apiHost.GetApiEndpoint();

            switch (endpoint)
            {
                case ApiEndpoint.Llama:
                    return new LlamaTextGenerator(httpClient, apiHost, logger);

                case ApiEndpoint.OpenAi:
                    return new OpenAiTextGenerator(httpClient, apiHost, logger);

                case ApiEndpoint.Gemini:
                    if (string.IsNullOrEmpty(apiKey))
                    {
                        Console.Error.WriteLine("Error: API key is required for Gemini.");
                        return null;
                    }
                    return new GeminiTextGenerator(httpClient, apiHost, apiKey, logger);

                default:
                    Console.Error.WriteLine("Error: Unsupported API endpoint.");
                    return null;
            }
        }

        public static IModelProvider CreateModelProvider(string apiHost, HttpClient httpClient, string apiKey = null, ILogger logger = null)
        {
            ApiEndpoint endpoint = apiHost.GetApiEndpoint();

            switch (endpoint)
            {
                case ApiEndpoint.OpenAi:
                    return new OpenAIModelProvider(httpClient, apiHost);
                case ApiEndpoint.Gemini:
                    return new GeminiModelProvider(httpClient, apiHost, apiKey);
                default:
                    return null;
            }
        }
    }
}
