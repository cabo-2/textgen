using System.Net.Http.Headers;
using Textgen;

class Program
{
    static async Task Main()
    {
        // API endpoint and API key
        string host = "https://api.openai.com/v1/chat/completions";
        string apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? "YOUR_API_KEY";

        using var httpClient = new HttpClient();
        if (!string.IsNullOrEmpty(apiKey))
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        // Create a text generator using the factory
        var generator = TextGeneratorFactory.CreateGenerator(host, httpClient);
        if (generator == null)
        {
            Console.WriteLine("Unsupported API endpoint.");
            return;
        }

        // Create default configuration for the generator
        var config = generator.CreateDefaultConfig();

        string model = "gpt-4o-mini";
        string systemPrompt = "You are a helpful assistant.";
        string userPrompt = "Hello!";

        // Generate text
        var result = await generator.GenerateTextAsync(model, userPrompt, systemPrompt, config, new OutputResult());

        Console.WriteLine(result.Completion);
    }
}
