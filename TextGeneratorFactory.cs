using System;

namespace textgen
{
    public static class TextGeneratorFactory
    {
        public static TextGenerator CreateGenerator(string apiHost, HttpClient httpClient)
        {
            ApiEndpoint endpoint = GetApiEndpoint(apiHost);

            switch (endpoint)
            {
                case ApiEndpoint.Llama:
                    return new LlamaTextGenerator(httpClient, apiHost);

                case ApiEndpoint.OpenAi:
                    return new OpenAiTextGenerator(httpClient, apiHost);

                default:
                    Console.Error.WriteLine("Error: Unsupported API endpoint specified.");
                    Console.Error.WriteLine($"The specified endpoint is: {apiHost}");
                    throw new NotSupportedException("apiHost");
            }
        }

        private static ApiEndpoint GetApiEndpoint(string apiHost)
        {
            if (apiHost.EndsWith("/completion"))
            {
                return ApiEndpoint.Llama;
            }
            if (apiHost.EndsWith("/v1/chat/completions"))
            {
                return ApiEndpoint.OpenAi;
            }
            return ApiEndpoint.Unsupported;
        }
    }

    public enum ApiEndpoint
    {
        Llama,
        OpenAi,
        Unsupported
    }
}