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
    public class Program
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
            var systemFileOption = app.Option("-S|--system-file <FNAME>", "System prompt from a file.", CommandOptionType.SingleValue);
            var formatOption = app.Option("-f|--format <FORMAT>", "Output format (text, detail, json)", CommandOptionType.SingleValue);
            formatOption.DefaultValue = "text";
            formatOption.Accepts().Values("text", "detail", "json");
            var outputOption = app.Option("-o|--output <FILE_PATH>", "Output file path (default is standard output).", CommandOptionType.SingleValue);
            var configOption = app.Option("-c|--config <FNAME>", "Parameter settings file (JSON).", CommandOptionType.SingleValue);
            configOption.Accepts().ExistingFile();

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
                var config = LoadConfig(configFile, cancellationToken);

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

                // Generate text
                var textGenerator = new TextGenerator(httpClient, openAIHost);
                var responseText = await textGenerator.GenerateTextAsync(model, prompt, systemPrompt, config, cancellationToken);

                // Create output object
                var outputResult = new OutputResult
                {
                    Date = DateTime.UtcNow.ToString("o"),
                    Host = openAIHost,
                    Model = model,
                    Config = config,
                    SystemPrompt = systemPrompt,
                    Prompt = prompt,
                    Completion = responseText
                };

                // Output result in the desired format
                string formattedOutput = outputResult.Format(format);
                if (!string.IsNullOrEmpty(outputFile))
                {
                    await File.WriteAllTextAsync(outputFile, formattedOutput, cancellationToken);
                }
                else
                {
                    Console.WriteLine(formattedOutput);
                }

                return 0;
            });

            return await app.ExecuteAsync(args);
        }

        private static Config LoadConfig(string configFile, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(configFile))
            {
                return new Config
                {
                    MaxTokens = 1200,
                    Seed = 0,
                    Temperature = 0.7,
                    TopP = 1
                };
            }

            var configContent = File.ReadAllText(configFile);
            dynamic config = JsonConvert.DeserializeObject(configContent);

            return new Config
            {
                MaxTokens = config.max_tokens ?? 1200,
                Seed = config.seed ?? 0,
                Temperature = config.temperature ?? 0.7,
                TopP = config.top_p ?? 1
            };
        }
    }

    public class OutputResult
    {
        public string Date { get; set; }
        public string Host { get; set; }
        public string Model { get; set; }
        public Config Config { get; set; }
        public string SystemPrompt { get; set; }
        public string Prompt { get; set; }
        public string Completion { get; set; }

        public string Format(string format)
        {
            switch (format?.ToLower())
            {
                case "detail":
                    return $"@date\n{Date}\n\n" +
                           $"@host\n{Host}\n\n" +
                           $"@model\n{Model}\n\n" +
                           $"@config\n" +
                           $"max_tokens={Config.MaxTokens}\n" +
                           $"seed={Config.Seed}\n" +
                           $"temperature={Config.Temperature}\n" +
                           $"top_p={Config.TopP}\n\n" +
                           $"@system-prompt\n{SystemPrompt}\n\n" +
                           $"@prompt\n{Prompt}\n\n" +
                           $"@completion\n{Completion}";

                case "json":
                    return JsonConvert.SerializeObject(this, Formatting.Indented); // Pretty format output

                default: // text
                    return Completion;
            }
        }
    }

    public class Config
    {
        public int MaxTokens { get; set; }
        public int Seed { get; set; }
        public double Temperature { get; set; }
        public double TopP { get; set; }
        public string Username { get; set; } = "user";
        public string AssistantName { get; set; } = "assistant";
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

        public async Task<string> GenerateTextAsync(string model, string prompt, string systemPrompt, Config config, CancellationToken cancellationToken = default)
        {
            var requestBody = new
            {
                model = model,
                messages = new[]
                {
                    new { role = "system", content = systemPrompt ?? "" },
                    new { role = config.Username, content = prompt }
                    //new { role = config.AssistantName, content = "" }
                },
                max_tokens = config.MaxTokens,
                seed = config.Seed,
                temperature = config.Temperature,
                top_p = config.TopP
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
