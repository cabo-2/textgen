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
            openAIHost = Environment.GetEnvironmentVariable("OPENAI_API_HOST") ?? "http://localhost:8081/completion";
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
            var promptFileOption = app.Option("-pf|--prompt-file <FNAME>", "Input message from a file.", CommandOptionType.SingleValue);
            var systemOption = app.Option("-s|--system <SYSTEM_PROMPT>", "System prompt directly.", CommandOptionType.SingleValue);
            var systemFileOption = app.Option("-sf|--system-file <FNAME>", "System prompt from a file.", CommandOptionType.SingleValue);
            var formatOption = app.Option("-f|--format <FORMAT>", "Output format (text, json)", CommandOptionType.SingleValue);
            formatOption.DefaultValue = "text";
            formatOption.Accepts().Values("text", "json");
            var outputOption = app.Option("-o|--output <FILE_PATH>", "Output file path (default is standard output).", CommandOptionType.SingleValue);
            var configOption = app.Option("-c|--config <FNAME>", "Parameter settings file (JSON).", CommandOptionType.SingleValue);
            configOption.Accepts().ExistingFile();
            var conversationLogOption = app.Option("-cl|--conversation-log <FNAME>", "File to read and maintain conversation logs.", CommandOptionType.SingleValue);
            conversationLogOption.Accepts().ExistingFile();

            app.HelpOption("-h|--help");
            app.VersionOption("-v|--version", "1.0.0");

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
                var conversationLog = string.IsNullOrEmpty(conversationLogFile) ? new OutputResult() : await OutputResult.LoadFromFileAsync(conversationLogFile, cancellationToken);

                // Validate inputs
                if (string.IsNullOrEmpty(model) || string.IsNullOrEmpty(prompt))
                {
                    Console.WriteLine("Error: Model and prompt must be specified.");
                    return 1;
                }

                // Generate text
                var textGenerator = new TextGenerator(httpClient, openAIHost);
                OutputResult outputResult = await textGenerator.GenerateTextAsync(model, prompt, systemPrompt, config, conversationLog, cancellationToken);

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
        public List<(string Prompt, string Completion)> History { get; set; } = new List<(string, string)>();

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
                    sb.Append($"n_predict={Config.NPredict}\n");
                    sb.Append($"seed={Config.Seed}\n");
                    sb.Append($"temperature={Config.Temperature}\n");
                    sb.Append($"top_k={Config.TopK}\n");
                    sb.Append($"top_p={Config.TopP}\n");
                    sb.Append($"min_p={Config.MinP}\n");
                    sb.Append($"presence_penalty={Config.PresencePenalty}\n");
                    sb.Append($"frequency_penalty={Config.FrequencyPenalty}\n");
                    sb.Append($"repeat_penalty={Config.RepeatPenalty}\n");
                    sb.Append($"stream={Config.Stream.ToString().ToLower()}\n");
                    sb.Append($"cache_prompt={Config.CachePrompt.ToString().ToLower()}\n");
                    sb.Append($"username={Config.Username}\n");
                    sb.Append($"assistant_name={Config.AssistantName}\n\n");
                    sb.Append($"@system-prompt\n{SystemPrompt}\n\n");

                    if (History.Count > 0)
                    {
                        for (int i = 0; i < History.Count; i++)
                        {
                            var history = History[i];
                            sb.Append($"@history-prompt_{i + 1}\n{history.Prompt}\n\n");
                            sb.Append($"@history-completion_{i + 1}\n{history.Completion}\n\n");
                        }
                    }

                    sb.Append($"@prompt\n{Prompt}\n\n");
                    sb.Append($"@completion\n{Completion}\n");

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
                                case "n_predict": config.NPredict = int.Parse(value); break;
                                case "seed": config.Seed = int.Parse(value); break;
                                case "temperature": config.Temperature = double.Parse(value); break;
                                case "top_k": config.TopK = int.Parse(value); break;
                                case "top_p": config.TopP = double.Parse(value); break;
                                case "min_p": config.MinP = double.Parse(value); break;
                                case "presence_penalty": config.PresencePenalty = double.Parse(value); break;
                                case "frequency_penalty": config.FrequencyPenalty = double.Parse(value); break;
                                case "repeat_penalty": config.RepeatPenalty = double.Parse(value); break;
                                case "stream": config.Stream = bool.Parse(value); break;
                                case "cache_prompt": config.CachePrompt = bool.Parse(value); break;
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
                else if (line.StartsWith("@prompt"))
                {
                    result.Prompt = lines[++i];
                }
                else if (line.StartsWith("@completion"))
                {
                    result.Completion = lines[++i];
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
                result.History.Add((prompt, completion));
            }

            return result;
        }
    }

    public class Config
    {
        public static readonly Config DefaultConfig = new Config
        {
            NPredict = 1200,
            Seed = 1337,
            Temperature = 0.7,
            TopK = 50,
            TopP = 0.9,
            MinP = 0.1,
            PresencePenalty = 0.0,
            FrequencyPenalty = 0.0,
            RepeatPenalty = 1.1,
            Stream = false,
            CachePrompt = true,
            Username = "user",
            AssistantName = "assistant"
        };

        public int NPredict { get; set; }
        public int Seed { get; set; }
        public double Temperature { get; set; }
        public int TopK { get; set; }
        public double TopP { get; set; }
        public double MinP { get; set; }
        public double PresencePenalty { get; set; }
        public double FrequencyPenalty { get; set; }
        public double RepeatPenalty { get; set; }
        public bool Stream { get; set; }
        public bool CachePrompt { get; set; }
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
                NPredict = config.n_predict ?? DefaultConfig.NPredict,
                Seed = config.seed ?? DefaultConfig.Seed,
                Temperature = config.temperature ?? DefaultConfig.Temperature,
                TopK = config.top_k ?? DefaultConfig.TopK,
                TopP = config.top_p ?? DefaultConfig.TopP,
                MinP = config.min_p ?? DefaultConfig.MinP,
                PresencePenalty = config.presence_penalty ?? DefaultConfig.PresencePenalty,
                FrequencyPenalty = config.frequency_penalty ?? DefaultConfig.FrequencyPenalty,
                RepeatPenalty = config.repeat_penalty ?? DefaultConfig.RepeatPenalty,
                Stream = config.stream ?? DefaultConfig.Stream,
                CachePrompt = config.cache_prompt ?? DefaultConfig.CachePrompt,
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

        public async Task<OutputResult> GenerateTextAsync(string model, string prompt, string systemPrompt, Config config, OutputResult conversationLog, CancellationToken cancellationToken = default)
        {
            if (config.Stream)
            {
                Console.Error.WriteLine("Warning: stream is set to true, but it is not supported. Setting to false.");
                config.Stream = false;
            }

            StringBuilder sb = new StringBuilder();
            var history = new List<(string, string)>();

            // Add system-prompt to request prompt
            if (!string.IsNullOrEmpty(systemPrompt))            
                sb.Append($"{systemPrompt}\n\n");

            // Add conversation history to request prompt
            foreach (var entry in conversationLog.History)
            {
                if (!string.IsNullOrEmpty(entry.Prompt))
                    sb.Append($"{config.Username}: {entry.Prompt}\n\n");

                if (!string.IsNullOrEmpty(entry.Completion))
                    sb.Append($"{config.AssistantName}: {entry.Completion}\n\n");

                if(!string.IsNullOrEmpty(entry.Prompt) || !string.IsNullOrEmpty(entry.Completion))
                    history.Add((entry.Prompt ?? "", entry.Completion ?? ""));
            }

            if (!string.IsNullOrEmpty(conversationLog.Prompt))
                sb.Append($"{config.Username}: {conversationLog.Prompt}\n\n");

            if (!string.IsNullOrEmpty(conversationLog.Completion))
                sb.Append($"{config.AssistantName}: {conversationLog.Completion}\n\n");

            if (!string.IsNullOrEmpty(conversationLog.Prompt) || !string.IsNullOrEmpty(conversationLog.Completion))
                history.Add((conversationLog.Prompt ?? "", conversationLog.Completion ?? ""));

            // Add prompt to request prompt
            sb.Append($"{config.Username}: {prompt}\n\n");
            sb.Append($"{config.AssistantName}: ");

            var requestBody = new
            {
                prompt = sb.ToString(),
                n_predict = config.NPredict,
                seed = config.Seed,
                temperature = config.Temperature,
                top_k = config.TopK,
                top_p = config.TopP,
                min_p = config.MinP,
                presence_penalty = config.PresencePenalty,
                frequency_penalty = config.FrequencyPenalty,
                repeat_penalty = config.RepeatPenalty,
                stream = config.Stream,
                cache_prompt = config.CachePrompt
            };

            Console.Write(sb.ToString());

            var jsonRequest = JsonConvert.SerializeObject(requestBody);
            var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(_apiHost, content, cancellationToken);
            response.EnsureSuccessStatusCode();

            var jsonResponse = await response.Content.ReadAsStringAsync(cancellationToken);
            dynamic result = JsonConvert.DeserializeObject(jsonResponse);

            if (result.stop != "true")
            {
                Console.Error.WriteLine("Warning: stop field is false.");
            }

            // Generate result data
            var outputResult = new OutputResult();
            outputResult.Date = DateTime.UtcNow.ToString("o");
            outputResult.Host = _apiHost;
            outputResult.Model = model;
            outputResult.Config = config;
            outputResult.SystemPrompt = systemPrompt;
            outputResult.History = history;
            outputResult.Prompt = prompt.Trim();
            outputResult.Completion = result.content.ToString().Trim();            

            return outputResult;
        }
    }
}