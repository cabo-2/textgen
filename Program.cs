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
            var formatOption = app.Option("-f|--format <FORMAT>", "Output format (text, json)", CommandOptionType.SingleValue);
            formatOption.DefaultValue = "text";
            formatOption.Accepts().Values("text", "json");
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
                var config = await Config.LoadConfigAsync(configFile, cancellationToken);

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
                // Always output to console
                Console.WriteLine(outputResult.Completion);

                return 0;
            });

            return await app.ExecuteAsync(args);
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
                case "json":
                    return JsonConvert.SerializeObject(this, Formatting.Indented); // Pretty format output

                default: // text
                    return $"@date\n{Date}\n\n" +
                           $"@host\n{Host}\n\n" +
                           $"@model\n{Model}\n\n" +
                           $"@config\n" +
                           $"max_tokens={Config.MaxTokens}\n" +
                           $"seed={Config.Seed}\n" +
                           $"temperature={Config.Temperature}\n" +
                           $"top_p={Config.TopP}\n" +
                           $"username={Config.Username}\n" +
                           $"assistant_name={Config.AssistantName}\n\n" +
                           $"@system-prompt\n{SystemPrompt}\n\n" +
                           $"@prompt\n{Prompt}\n\n" +
                           $"@completion\n{Completion}";
            }
        }

        public static async Task<OutputResult> LoadFromFileAsync(string filePath, CancellationToken cancellationToken)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("File not found", filePath);
            }

            var fileContent = await File.ReadAllTextAsync(filePath, cancellationToken);
            return filePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ? LoadFromJson(fileContent) : LoadFromText(fileContent);
        }

        private static OutputResult LoadFromJson(string jsonContent)
        {
            return JsonConvert.DeserializeObject<OutputResult>(jsonContent);
        }

        private static OutputResult LoadFromText(string textContent)
        {
            var lines = textContent.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            var result = new OutputResult();

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];

                if (line.StartsWith("@date"))
                {
                    result.Date = lines[++i];
                }
                else if (line.StartsWith("@host"))
                {
                    result.Host = lines[++i];
                }
                else if (line.StartsWith("@config"))
                {
                    var config = new Config();
                    while (i + 1 < lines.Length && !lines[i + 1].StartsWith("@"))
                    {
                        var configLine = lines[++i].Split(new[] { '=' }, 2);
                        if (configLine.Length == 2)
                        {
                            var key = configLine[0].Trim();
                            var value = configLine[1].Trim();
                            switch (key)
                            {
                                case "max_tokens": config.MaxTokens = int.Parse(value); break;
                                case "seed": config.Seed = int.Parse(value); break;
                                case "temperature": config.Temperature = double.Parse(value); break;
                                case "top_p": config.TopP = double.Parse(value); break;
                                case "username": config.Username = value; break;
                                case "assistant_name": config.AssistantName = value; break;
                            }
                        }
                    }
                    result.Config = config;
                }
                else if (line.StartsWith("@model"))
                {
                    result.Model = lines[++i];
                }
                else if (line.StartsWith("@system-prompt"))
                {
                    result.SystemPrompt = lines[++i];
                }
                else if (line.StartsWith("@prompt"))
                {
                    result.Prompt = lines[++i];
                }
                else if (line.StartsWith("@completion"))
                {
                    result.Completion = lines[++i];
                }
            }

            return result;
        }
    }

    public class Config
    {
        public static readonly Config DefaultConfig = new Config
        {
            MaxTokens = 1200,
            Seed = 0,
            Temperature = 0.7,
            TopP = 1,
            Username = "user",
            AssistantName = "assistant"
        };

        public int MaxTokens { get; set; }
        public int Seed { get; set; }
        public double Temperature { get; set; }
        public double TopP { get; set; }
        public string Username { get; set; }
        public string AssistantName { get; set; }

        public static async Task<Config> LoadConfigAsync(string configFile, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(configFile))
            {
                return DefaultConfig; // Use default values
            }

            var configContent = await File.ReadAllTextAsync(configFile, cancellationToken);
            dynamic config = JsonConvert.DeserializeObject(configContent);

            return new Config
            {
                MaxTokens = config.max_tokens ?? DefaultConfig.MaxTokens,
                Seed = config.seed ?? DefaultConfig.Seed,
                Temperature = config.temperature ?? DefaultConfig.Temperature,
                TopP = config.top_p ?? DefaultConfig.TopP,
                Username = config.username ?? DefaultConfig.Username,
                AssistantName = config.assistant_name ?? DefaultConfig.AssistantName
            };
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

        public async Task<string> GenerateTextAsync(string model, string prompt, string systemPrompt, Config config, CancellationToken cancellationToken = default)
        {
            var requestBody = new
            {
                model = model,
                messages = new[]
                {
                    new { role = "system", content = systemPrompt ?? "" },
                    new { role = config.Username, content = prompt }
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