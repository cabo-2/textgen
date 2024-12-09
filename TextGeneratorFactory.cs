using System;

namespace textgen
{
    public static class TextGeneratorFactory
    {
        public static TextGenerator CreateGenerator(string apiHost, HttpClient httpClient, string apiKey = null)
        {
            ApiEndpoint endpoint = apiHost.GetApiEndpoint();

            switch (endpoint)
            {
                case ApiEndpoint.Llama:
                    return new LlamaTextGenerator(httpClient, apiHost);

                case ApiEndpoint.OpenAi:
                    return new OpenAiTextGenerator(httpClient, apiHost);

                case ApiEndpoint.Gemini:
                    if (string.IsNullOrEmpty(apiKey))
                    {
                        Console.Error.WriteLine("Error: API key is required for Gemini API.");
                        throw new ArgumentException("apiKey");
                    }
                    return new GeminiTextGenerator(httpClient, apiHost, apiKey);

                default:
                    Console.Error.WriteLine("Error: Unsupported API endpoint specified.");
                    Console.Error.WriteLine($"The specified endpoint is: {apiHost}");
                    throw new NotSupportedException("apiHost");
            }
        }

        public static IModelProvider CreateModelProvider(string apiHost, HttpClient httpClient, string apiKey = null)
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
