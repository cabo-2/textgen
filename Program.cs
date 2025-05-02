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
        private static readonly string apiKey;

        static Program()
        {
            openAIHost = Environment.GetEnvironmentVariable("OPENAI_API_HOST") ?? "http://localhost:8081/completion";
            apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            if (!string.IsNullOrEmpty(apiKey))
            {
                if (openAIHost.GetApiEndpoint() != ApiEndpoint.Gemini)
                    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            }
            httpClient.Timeout = Timeout.InfiniteTimeSpan;
        }

        static async Task<int> Main(string[] args)
        {
            var app = new CommandLineApplication
            {
                Name = "textgen",
                Description = "A simple console app to generate text using OpenAI's API."
            };

            // Command-line arguments
            var modelOption = app.Option("-m|--model <MODEL>", "Specify the model to use (gpt-4o, gpt-4o-mini).", CommandOptionType.SingleValue);
            var promptOption = app.Option("-p|--prompt <PROMPT>", "Input message directly.", CommandOptionType.SingleValue);
            var promptFileOption = app.Option("-P|--prompt-file <FNAME>", "Input message from a file.", CommandOptionType.SingleValue);
            var systemOption = app.Option("-s|--system <SYSTEM_PROMPT>", "System prompt directly.", CommandOptionType.SingleValue);
            var systemFileOption = app.Option("-S|--system-file <FNAME>", "System prompt from a file.", CommandOptionType.SingleValue);
            var formatOption = app.Option("-f|--format <FORMAT>", "Output format (text, json)", CommandOptionType.SingleValue);
            formatOption.DefaultValue = "text";
            formatOption.Accepts().Values("text", "json");
            var outputOption = app.Option("-o|--output <FILE_PATH>", "Output file path (default is standard output).", CommandOptionType.SingleValue);
            var outputDirectoryOption = app.Option("-O|--output-dir <DIR_PATH>", "Directory to save output file.", CommandOptionType.SingleValue);
            var configOption = app.Option("-c|--config <FNAME>", "Parameter settings file (text, json).", CommandOptionType.SingleValue);
            configOption.Accepts().ExistingFile();
            var conversationLogOption = app.Option("-l|--conversation-log <FNAME>", "File to read and maintain conversation logs.", CommandOptionType.SingleValue);
            var conversationLogDirectoryOption = app.Option("-L|--conversation-log-dir <DIR_PATH>", "Directory to read conversation logs from.", CommandOptionType.SingleValue);
            conversationLogDirectoryOption.Accepts().ExistingDirectory();
            var queryOption = app.Option("-q|--query", "Query and list available model names.", CommandOptionType.NoValue);

            app.HelpOption("-h|--help");
            app.VersionOption("-v|--version", "1.0.0");

            app.OnExecuteAsync(async (cancellationToken) =>
            {
                // If query option is specified, handle it and exit
                if (queryOption.HasValue())
                {
                    var provider = TextGeneratorFactory.CreateModelProvider(openAIHost, httpClient, apiKey);
                    if (provider == null)
                    {
                        Console.WriteLine("Unsupported endpoint.");
                        return 0;
                    }

                    var models = await provider.GetModelsAsync();
                    if (models == null)
                        return 1;

                    if (models.Count > 0)
                        foreach (var m in models)
                            Console.WriteLine(m);
                    else
                        Console.WriteLine("No models found.");

                    return 0;
                }

                // Existing options processing
                string model = modelOption.Value();
                string prompt = promptOption.Value();
                string promptFile = promptFileOption.Value();
                string system = systemOption.Value();
                string systemFile = systemFileOption.Value();
                string format = formatOption.Value();
                string outputFile = outputOption.Value();
                string outputDirectory = outputDirectoryOption.Value();
                string configFile = configOption.Value();
                string conversationLogFile = conversationLogOption.Value();
                string conversationLogDirectory = conversationLogDirectoryOption.Value();

                // Check for conflicting options
                if (!string.IsNullOrEmpty(conversationLogDirectory) && (!string.IsNullOrEmpty(outputFile) || !string.IsNullOrEmpty(outputDirectory)))
                {
                    Console.WriteLine("Error: Cannot specify both -L with either -o or -O options.");
                    return 1;
                }

                // Determine the text generator and configuration to use
                var textGenerator = TextGeneratorFactory.CreateGenerator(openAIHost, httpClient, apiKey);
                if (textGenerator == null)
                    return 1;
                    
                var defaultConfig = textGenerator.CreateDefaultConfig();

                // Load parameters from config file if specified
                var config = await defaultConfig.LoadConfigAsync(configFile, cancellationToken);


                bool hasConvLog = !string.IsNullOrEmpty(conversationLogFile);
                bool hasConvDir = !string.IsNullOrEmpty(conversationLogDirectory);
                bool hasPromptInput = promptOption.HasValue() || promptFileOption.HasValue();
                if ((hasConvLog || hasConvDir) && !hasPromptInput)
                {
                    return await RunInteractiveAsync(
                        textGenerator,
                        defaultConfig,
                        config,
                        model,
                        system,
                        format,
                        conversationLogFile,
                        conversationLogDirectory,
                        cancellationToken);
                }

                // Load prompt from file if specified
                if (!string.IsNullOrEmpty(promptFile))
                {
                    prompt = await File.ReadAllTextAsync(promptFile, cancellationToken);
                }

                // Load system prompt from file if specified
                if (!string.IsNullOrEmpty(systemFile))
                {
                    system = await File.ReadAllTextAsync(systemFile, cancellationToken);
                }

                OutputResult conversationLog = new OutputResult();

                // Load conversation history either from a directory or a specific file
                if (!string.IsNullOrEmpty(conversationLogDirectory))
                {
                    var files = Directory.GetFiles(conversationLogDirectory, "textgen_log_*.*")
                        .OrderByDescending(f => f)
                        .ToList();

                    if (files.Count > 0)
                    {
                        string latestFile = files[0];
                        conversationLog = await OutputResult.LoadFromFileAsync(latestFile, defaultConfig, cancellationToken);
                    }
                }
                else if (!string.IsNullOrEmpty(conversationLogFile))
                {
                    conversationLog = await OutputResult.LoadFromFileAsync(conversationLogFile, defaultConfig, cancellationToken);
                }

                // Validate inputs
                if (string.IsNullOrEmpty(model) || string.IsNullOrEmpty(prompt))
                {
                    Console.WriteLine("Error: Model and prompt must be specified.");
                    return 1;
                }

                PromptSet promptSet = PromptSet.Create(prompt);
                OutputResult outputResult = conversationLog.DeepClone();
                foreach (var input in promptSet.Prompts)
                {
                    outputResult = await textGenerator.GenerateTextAsync(model, input, system, config, outputResult, cancellationToken);
                    // Always output to console
                    Console.WriteLine(outputResult.Completion);
                }

                // Output result in the desired format
                string formattedOutput = outputResult.Format(format);
                if (!string.IsNullOrEmpty(outputFile))
                {
                    await File.WriteAllTextAsync(outputFile, formattedOutput, cancellationToken);
                }
                else if (!string.IsNullOrEmpty(outputDirectory))
                {
                    // Ensure the directory exists
                    Directory.CreateDirectory(outputDirectory);

                    // Create the file name
                    string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                    string fileExtension = format == "json" ? "json" : "txt";
                    string fileName = $"textgen_log_{timestamp}.{fileExtension}";
                    string fullPath = Path.Combine(outputDirectory, fileName);

                    // Write the formatted output to the file
                    await File.WriteAllTextAsync(fullPath, formattedOutput, cancellationToken);
                }

                return 0;
            });

            return await app.ExecuteAsync(args);
        }

        private static async Task<int> RunInteractiveAsync(
            TextGenerator textGenerator,
            IConfig defaultConfig,
            IConfig config,
            string model,
            string system,
            string format,
            string convLogFile,
            string convLogDir,
            CancellationToken cancellationToken)
        {
            OutputResult conversation = new OutputResult();
            if (!string.IsNullOrEmpty(convLogFile))
            {
                if (!File.Exists(convLogFile))
                {
                    Console.WriteLine($"Log file not found, creating new: {convLogFile}");
                    var dir = Path.GetDirectoryName(convLogFile);
                    if (!string.IsNullOrEmpty(dir))
                        Directory.CreateDirectory(dir);
                }
                else
                {
                    conversation = await OutputResult.LoadFromFileAsync(convLogFile, defaultConfig, cancellationToken);
                }
            }
            else if (!string.IsNullOrEmpty(convLogDir))
            {
                var files = Directory.GetFiles(convLogDir, "textgen_log_*.*")
                    .OrderByDescending(f => f)
                    .ToList();
                if (files.Count > 0)
                {
                    conversation = await OutputResult.LoadFromFileAsync(files[0], defaultConfig, cancellationToken);
                }
            }

            string userLabel, assistantLabel;
            if (config is LlamaConfig lc)
            {
                userLabel = lc.Username;
                assistantLabel = lc.AssistantName;
            }
            else if (config is OpenAiConfig oac)
            {
                userLabel = oac.Username;
                assistantLabel = oac.AssistantName;
            }
            else
            {
                userLabel = "User";
                assistantLabel = "Assistant";
            }

            bool cancelled = false;
            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;
                cancelled = true;
            };

            Console.WriteLine("Conversation mode started (ends with exit/quit, also ends with Ctrl+C)\n");

            foreach (var (p, c) in conversation.History)
            {
                Console.WriteLine($"{userLabel}: {p}");
                Console.WriteLine($"{assistantLabel}: {c}");
            }
            Console.WriteLine();
            
            while (!cancelled)
            {
                Console.Write($"{userLabel}> ");
                string input = Console.ReadLine();
                if (input == null) break;
                input = input.Trim();
                if (input.Equals("exit", StringComparison.OrdinalIgnoreCase)
                 || input.Equals("quit", StringComparison.OrdinalIgnoreCase))
                    break;
                if (string.IsNullOrEmpty(input)) continue;

                OutputResult output;
                try
                {
                    output = await textGenerator.GenerateTextAsync(
                        model, input, system, config, conversation, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Error: {ex.Message}");
                    continue;
                }

                Console.WriteLine($"{assistantLabel}: {output.Completion}");
                conversation = output;

                if (!string.IsNullOrEmpty(convLogFile))
                {
                    await File.WriteAllTextAsync(convLogFile, conversation.Format(format), cancellationToken);
                }
                else if (!string.IsNullOrEmpty(convLogDir))
                {
                    Directory.CreateDirectory(convLogDir);
                    string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                    string ext = format.Equals("json", StringComparison.OrdinalIgnoreCase) ? "json" : "txt";
                    string fileName = $"textgen_log_{timestamp}.{ext}";
                    var outPath = Path.Combine(convLogDir, fileName);
                    await File.WriteAllTextAsync(outPath, conversation.Format(format), cancellationToken);
                }
            }

            Console.WriteLine("\nsession ended");

            if (!string.IsNullOrEmpty(convLogDir))
            {
                Directory.CreateDirectory(convLogDir);
                string ts = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                string ext = format.Equals("json", StringComparison.OrdinalIgnoreCase) ? "json" : "txt";
                string outFile = Path.Combine(convLogDir, $"textgen_log_{ts}.{ext}");
                await File.WriteAllTextAsync(outFile, conversation.Format(format), cancellationToken);
                Console.WriteLine($"Log saved: {outFile}");
            }

            return 0;
        }
    }
}
