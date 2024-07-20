using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using Newtonsoft.Json;

namespace textgen
{
    class Program
    {
        private static string openAIHost;
        private static readonly HttpClient httpClient;
        static Program()
        {
            openAIHost = Environment.GetEnvironmentVariable("OPENAI_API_HOST") ?? "http://localhost:8080/v1/chat/completions";
            httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Environment.GetEnvironmentVariable("OPENAI_API_KEY"));
        }

        static async Task<int> Main(string[] args)
        {
            var app = new CommandLineApplication
            {
                Name = "textgen",
                Description = "A simple console app to generate text using OpenAI's API."
            };

            // Command-line arguments
            var modelOption = app.Option("-m|--model <MODEL>", "Specify the model to use (gpt-3.5-turbo, gpt-4).", CommandOptionType.SingleValue);
            var promptOption = app.Option("-p|--prompt <PROMPT>", "Input message directly.", CommandOptionType.SingleValue);
            var promptFileOption = app.Option("-P|--prompt-file <FNAME>", "Input message from a file.", CommandOptionType.SingleValue);
            var systemOption = app.Option("-s|--system <SYSTEM_PROMPT>", "System prompt directly.", CommandOptionType.SingleValue);
            var systemFileOption = app.Option("-S|--system-file <FNAME>", "System prompt from a file.", CommandOptionType.SingleValue);
            var outputOption = app.Option("-o|--output <FILE_PATH>", "Output file path (default is standard output).", CommandOptionType.SingleValue);

            app.HelpOption("-h|--help");
            app.VersionOption("-v|--version", "1.0.0");

            app.Command("-l|--list-model", cmd =>
            {
                cmd.Description = "List available models.";
                cmd.OnExecute(() =>
                {
                    Console.WriteLine("Available models:");
                    Console.WriteLine("- gpt-3.5-turbo");
                    Console.WriteLine("- gpt-4");
                    return 0;
                });
            });

            app.OnExecuteAsync(async (cancellationToken) =>
            {
                string model = null;
                string prompt = null;
                string promptFile = null;
                string systemPrompt = null;
                string systemPromptFile = null;
                string outputFile = null;

                // Parse options
                if (modelOption.HasValue())
                    model = modelOption.Value();
                if (promptOption.HasValue())
                    prompt = promptOption.Value();
                if (promptFileOption.HasValue())
                    promptFile = promptFileOption.Value();
                if (systemOption.HasValue())
                    systemPrompt = systemOption.Value();
                if (systemFileOption.HasValue())
                    systemPromptFile = systemFileOption.Value();
                if (outputOption.HasValue())
                    outputFile = outputOption.Value();

                // Load prompt from file if specified
                if (!string.IsNullOrEmpty(promptFile))
                {
                    prompt = await File.ReadAllTextAsync(promptFile, cancellationToken);
                }

                // Load system prompt from file if specified
                if (!string.IsNullOrEmpty(systemPromptFile))
                {
                    systemPrompt = await File.ReadAllTextAsync(systemPromptFile, cancellationToken);
                }

                Console.WriteLine("model:" + model);
                Console.WriteLine("prompt:" + prompt);

                // Validate inputs
                if (string.IsNullOrEmpty(model) || string.IsNullOrEmpty(prompt))
                {
                    Console.WriteLine("Error: Model and prompt must be specified.");
                    return 1;
                }

                // Call OpenAI API
                var responseText = await CallOpenAIAsync(model, prompt, systemPrompt);

                // Output result
                if (!string.IsNullOrEmpty(outputFile))
                {
                    await File.WriteAllTextAsync(outputFile, responseText, cancellationToken);
                }
                else
                {
                    Console.WriteLine(responseText);
                }

                return 0;
            });

            return await app.ExecuteAsync(args);
        }

        private static async Task<string> CallOpenAIAsync(string model, string prompt, string systemPrompt, CancellationToken cancellationToken = default)
        {            
            var requestBody = new
            {
                model = model,
                messages = new[]
                {
                    new { role = "system", content = systemPrompt ?? "" },
                    new { role = "user", content = prompt }
                }
            };

            var jsonRequest = JsonConvert.SerializeObject(requestBody);
            var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync(openAIHost, content, cancellationToken);
            response.EnsureSuccessStatusCode();

            var jsonResponse = await response.Content.ReadAsStringAsync(cancellationToken);
            dynamic result = JsonConvert.DeserializeObject(jsonResponse);

            return result.choices[0].message.content.ToString();
        }
    }
}