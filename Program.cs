using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
            var conversationLogOption = app.Option("--conversation-log <FNAME>", "File to read and maintain conversation logs.", CommandOptionType.SingleValue);
            conversationLogOption.Accepts().ExistingFile();

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
                string conversationLogFile = conversationLogOption.Value();

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

                // Load conversation history from log file
                var outputResult = string.IsNullOrEmpty(conversationLogFile) ? new OutputResult() : await OutputResult.LoadFromFileAsync(conversationLogFile, cancellationToken);

                // Validate inputs
                if (string.IsNullOrEmpty(model) || string.IsNullOrEmpty(prompt))
                {
                    Console.WriteLine("Error: Model and prompt must be specified.");
                    return 1;
                }

                // Generate text
                var textGenerator = new TextGenerator(httpClient, openAIHost);
                var responseText = await textGenerator.GenerateTextAsync(model, prompt, systemPrompt, config, outputResult.History, cancellationToken);

                // Update output result with new data
                outputResult.Date = DateTime.UtcNow.ToString("o");
                outputResult.Host = openAIHost;
                outputResult.Model = model;
                outputResult.Config = config;
                outputResult.SystemPrompt = systemPrompt;
                outputResult.Prompt = prompt;
                outputResult.Completion = responseText;

                // Add new history entries
                outputResult.History.Add(new KeyValuePair<string, string>(prompt, responseText));

                // Output result in the desired format
                string formattedOutput = outputResult.Format(format);
                if (!string.IsNullOrEmpty(outputFile))
                {
                    await File.WriteAllTextAsync(outputFile, formattedOutput, cancellationToken);
                }
                // Always output to console
                Console.WriteLine(outputResult.Completion);

                // Write to conversation log file
                if (!string.IsNullOrEmpty(conversationLogFile))
                {
                    await File.WriteAllTextAsync(conversationLogFile, formattedOutput, cancellationToken);
                }

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
        public List<KeyValuePair<string, string>> History { get; set; } = new List<KeyValuePair<string, string>>();

        public string Format(string format)
        {
            switch (format?.ToLower())
            {
                case "json":
                    return JsonConvert.SerializeObject(this, Formatting.Indented); // Pretty format output

                default: // text
                    var sb = new StringBuilder();
                    sb.Append($"@date\n{Date}\n\n");
                    sb.Append($"@host\n{Host}\n\n");
                    sb.Append($"@model\n{Model}\n\n");
                    sb.Append($"@config\n");
                    sb.Append($"max_tokens={Config.MaxTokens}\n");
                    sb.Append($"seed={Config.Seed}\n");
                    sb.Append($"temperature={Config.Temperature}\n");
                    sb.Append($"top_p={Config.TopP}\n");
                    sb.Append($"username={Config.Username}\n");
                    sb.Append($"assistant_name={Config.AssistantName}\n\n");
                    sb.Append($"@system-prompt\n{SystemPrompt}\n\n");

                    if (History.Count > 0)
                    {
                        for (int i = 0; i < History.Count; i++)
                        {
                            var history = History[i];
                            sb.Append($"@history-prompt_{i + 1}\n{history.Key}\n\n");
                            sb.Append($"@history-completion_{i + 1}\n{history.Value}\n\n");
                        }
                    }

                    sb.Append($"@prompt\n{Prompt}\n\n");
                    sb.Append($"@completion\n{Completion}");

                    return sb.ToString();
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

            Dictionary<int, string> prompts = new Dictionary<int, string>();
            Dictionary<int, string> completions = new Dictionary<int, string>();

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
                else if (line.StartsWith("@history-prompt"))
                {
                    var index = int.Parse(line.Split('_')[1]);
                    prompts[index] = lines[++i];
                }
                else if (line.StartsWith("@history-completion"))
                {
                    var index = int.Parse(line.Split('_')[1]);
                    completions[index] = lines[++i];
                }
            }

            // Merge prompts and completions
            var maxIndex = Math.Max(prompts.Count > 0 ? prompts.Keys.Max() : 0, completions.Count > 0 ? completions.Keys.Max() : 0);
            for (int j = 1; j <= maxIndex; j++)
            {
                string prompt = null;
                string completion = null;
                prompts.TryGetValue(j, out prompt);
                completions.TryGetValue(j, out completion);
                result.History.Add(new KeyValuePair<string, string>(prompt, completion));
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

        public async Task<string> GenerateTextAsync(string model, string prompt, string systemPrompt, Config config, List<KeyValuePair<string, string>> history, CancellationToken cancellationToken = default)
        {
            var messages = new List<dynamic>
            {
                new { role = "system", content = systemPrompt ?? "" }
            };

            // Add history to messages
            foreach (var entry in history)
            {
                if (string.IsNullOrEmpty(entry.Key))
                    messages.Add(new { role = config.Username, content = entry.Key });

                if (string.IsNullOrEmpty(entry.Value))
                    messages.Add(new { role = config.AssistantName, content = entry.Value });
            }

            messages.Add(new { role = config.Username, content = prompt }); // current user prompt

            var requestBody = new
            {
                model = model,
                messages = messages,
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