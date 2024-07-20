using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using Newtonsoft.Json;

namespace textgen
{
    class Program
    {
        private static readonly HttpClient httpClient = new HttpClient();
        private static string openAIHost;

        static Program()
        {
            openAIHost = Environment.GetEnvironmentVariable("OPENAI_API_HOST") ?? "http://localhost:8080/v1/chat/completions";
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
            systemOption.DefaultValue = "";
            var systemFileOption = app.Option("-S|--system-file <FNAME>", "System prompt from a file.", CommandOptionType.SingleValue);
            var formatOption = app.Option("-f|--format <FORMAT>", "Output format (text, detail, json)", CommandOptionType.SingleValue);
            formatOption.DefaultValue = "text";
            var outputOption = app.Option("-o|--output <FILE_PATH>", "Output file path (default is standard output).", CommandOptionType.SingleValue);
            var configOption = app.Option("-c|--config <FNAME>", "Parameter settings file (JSON).", CommandOptionType.SingleValue);

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
                string model = modelOption.Value();
                string prompt = promptOption.Value();
                string promptFile = promptFileOption.Value();
                string systemPrompt = systemOption.Value();
                string systemPromptFile = systemFileOption.Value();
                string format = formatOption.Value();
                string outputFile = outputOption.Value();
                string configFile = configOption.Value();

                // Load parameters from config file if specified
                int maxTokens = 1200;
                int seed = 0;
                double temperature = 0.7;
                double topP = 1;

                if (!string.IsNullOrEmpty(configFile))
                {
                    var configContent = await File.ReadAllTextAsync(configFile, cancellationToken);
                    dynamic config = JsonConvert.DeserializeObject(configContent);
                    maxTokens = config.max_tokens ?? maxTokens;
                    seed = config.seed ?? seed;
                    temperature = config.temperature ?? temperature;
                    topP = config.top_p ?? topP;
                }

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

                // Validate inputs
                if (string.IsNullOrEmpty(model) || string.IsNullOrEmpty(prompt))
                {
                    Console.WriteLine("Error: Model and prompt must be specified.");
                    return 1;
                }

                // Use TextGenerator to call OpenAI API
                var textGenerator = new TextGenerator(httpClient, openAIHost);
                var responseText = await textGenerator.GenerateTextAsync(model, prompt, systemPrompt, maxTokens, seed, temperature, topP, cancellationToken);

                // Output result
                if (!string.IsNullOrEmpty(outputFile))
                {
                    string formattedOutput = FormatOutput(format, model, prompt, systemPrompt, responseText, maxTokens, seed, temperature, topP);
                    await File.WriteAllTextAsync(outputFile, formattedOutput, cancellationToken);
                }
                else
                {
                    string formattedOutput = FormatOutput(format, model, prompt, systemPrompt, responseText, maxTokens, seed, temperature, topP);
                    Console.WriteLine(formattedOutput);
                }

                return 0;
            });

            return await app.ExecuteAsync(args);
        }

        private static string FormatOutput(string format, string model, string prompt, string systemPrompt, string completion, int maxTokens, int seed, double temperature, double topP)
        {
            var date = DateTime.Now.ToString("o"); // ISO 8601 format date
            var host = openAIHost; // Request URI

            switch (format?.ToLower())
            {
                case "detailed":
                    return $"@date\n{date}\n\n" +
                           $"@host\n{host}\n\n" +
                           $"@model\n{model}\n\n" +
                           $"@config\n\n" +
                           $"max_tokens={maxTokens}\n" +
                           $"seed={seed}\n" +
                           $"temperature={temperature}\n" +
                           $"top_p={topP}\n\n" +
                           $"@system-prompt\n{systemPrompt}\n\n" +
                           $"@prompt\n{prompt}\n\n" +
                           $"@completion\n{completion}";

                case "json":
                    var jsonOutput = new
                    {
                        date = date,
                        host = host,
                        model = model,
                        config = new
                        {
                            max_tokens = maxTokens,
                            seed = seed,
                            temperature = temperature,
                            top_p = topP
                        },
                        system_prompt = systemPrompt,
                        prompt = prompt,
                        completion = completion
                    };
                    return JsonConvert.SerializeObject(jsonOutput, Formatting.Indented); // Pretty format output

                default: // text
                    return completion;
            }
        }
    }

    class TextGenerator
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiHost;

        public TextGenerator(HttpClient httpClient, string apiHost)
        {
            _httpClient = httpClient;
            _apiHost = apiHost;
        }

        public async Task<string> GenerateTextAsync(string model, string prompt, string systemPrompt, int maxTokens, int seed, double temperature, double topP, CancellationToken cancellationToken = default)
        {
            var requestBody = new
            {
                model = model,
                messages = new[]
                {
                    new { role = "system", content = systemPrompt ?? "" },
                    new { role = "user", content = prompt }
                },
                max_tokens = maxTokens,
                seed = seed,
                temperature = temperature,
                top_p = topP
            };

            var jsonRequest = JsonConvert.SerializeObject(requestBody);
            var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(_apiHost, content, cancellationToken);
            response.EnsureSuccessStatusCode();

            var jsonResponse = await response.Content.ReadAsStringAsync(cancellationToken);
            dynamic result = JsonConvert.DeserializeObject(jsonResponse);

            return result.choices[0].message.content.ToString();
        }
    }
}