using System.Net.Http.Headers;
using McMaster.Extensions.CommandLineUtils;

namespace Textgen
{
    public class Program
    {
        private const string DefaultApiHost = "http://localhost:8081/completion";

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
            var restoreCacheOption = app.Option("-rc|--restore-cache <FNAME>", "Restore llama-server cache before sending chat messages.", CommandOptionType.SingleValue);
            var saveCacheOption = app.Option("-sc|--save-cache <FNAME>", "Save llama-server cache after sending chat messages.", CommandOptionType.SingleValue);
            var imageOption = app.Option("-i|--image <FILE>", "Attach an image file (repeatable).", CommandOptionType.MultipleValue);
            var visionOnlyOption = app.Option("--vision-only", "Send images without a text prompt.", CommandOptionType.NoValue);
            var queryOption = app.Option("-q|--query", "Query and list available model names.", CommandOptionType.NoValue);
            var debugOption = app.Option("-d|--debug:<FILE_PATH>", "Real-time API logging.  No value -> stderr.  File path -> append log file.", CommandOptionType.SingleOrNoValue);

            app.HelpOption("-h|--help");
            app.VersionOption("-v|--version", "1.0.0");

            app.OnExecuteAsync(async (cancellationToken) =>
            {
                // Create ILogger (if any)
                ILogger logger = null;
                if (debugOption.HasValue())
                {
                    var debugFile = debugOption.Value();
                    if (!string.IsNullOrEmpty(debugFile))
                        logger = new FileLogger(debugFile);
                    else
                        logger = new ConsoleLogger();
                }

                // HttpClient w/ optional LoggingHandler
                HttpMessageHandler inner = new HttpClientHandler();
                if (logger != null) inner = new LoggingHandler(inner, logger);
                var httpClient = new HttpClient(inner) { Timeout = Timeout.InfiniteTimeSpan };

                var apiHost = ResolveApiHost(out var usedLegacyHostEnv);
                if (usedLegacyHostEnv)
                {
                    LogWarning(logger, "OPENAI_API_HOST is deprecated; please migrate to TEXTGEN_API_HOST.");
                }

                var apiKey = ResolveApiKey(apiHost, logger);
                var apiEndpoint = apiHost.GetApiEndpoint();

                if (!string.IsNullOrEmpty(apiKey) && apiEndpoint != ApiEndpoint.Gemini)
                    httpClient.DefaultRequestHeaders.Authorization =
                        new AuthenticationHeaderValue("Bearer", apiKey);

                var cacheOptions = new CacheActionOptions(restoreCacheOption.Value(), saveCacheOption.Value());
                var isOfficialOpenAiHost = apiHost.IsOfficialOpenAiHost();

                if (restoreCacheOption.HasValue() && string.IsNullOrWhiteSpace(cacheOptions.RestoreFile))
                {
                    Console.Error.WriteLine("Error: --restore-cache requires a filename.");
                    return 1;
                }

                if (saveCacheOption.HasValue() && string.IsNullOrWhiteSpace(cacheOptions.SaveFile))
                {
                    Console.Error.WriteLine("Error: --save-cache requires a filename.");
                    return 1;
                }

                if (cacheOptions.HasAny)
                {
                    bool cacheSupported = apiEndpoint == ApiEndpoint.Llama
                        || (apiEndpoint == ApiEndpoint.OpenAi && !isOfficialOpenAiHost);

                    if (!cacheSupported)
                    {
                        if (apiEndpoint == ApiEndpoint.OpenAi)
                        {
                            Console.Error.WriteLine("Error: Cache save/restore options are not allowed with https://api.openai.com/ hosts.");
                        }
                        else
                        {
                            Console.Error.WriteLine("Error: Cache save/restore options are only supported with llama-server hosts.");
                        }
                        return 1;
                    }
                }

                // If query option is specified, handle it and exit
                if (queryOption.HasValue())
                {
                    var provider = TextGeneratorFactory.CreateModelProvider(apiHost, httpClient, apiKey, logger);
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
                var imagePaths = imageOption.Values ?? new List<string>();
                bool visionOnly = visionOnlyOption.HasValue();

                // Check for conflicting options
                if (!string.IsNullOrEmpty(conversationLogDirectory) && (!string.IsNullOrEmpty(outputFile) || !string.IsNullOrEmpty(outputDirectory)))
                {
                    Console.WriteLine("Error: Cannot specify both -L with either -o or -O options.");
                    return 1;
                }

                // Determine the text generator and configuration to use
                var textGenerator = TextGeneratorFactory.CreateGenerator(apiHost, httpClient, apiKey, logger);
                if (textGenerator == null)
                    return 1;

                var defaultConfig = textGenerator.CreateDefaultConfig();

                // Load parameters from config file if specified
                var config = await defaultConfig.LoadConfigAsync(configFile, cancellationToken);

                var cacheCoordinator = CacheActionCoordinatorFactory.Create(apiEndpoint, apiHost, httpClient, cacheOptions, logger);

                try
                {
                    await cacheCoordinator.BeforeChatAsync(cancellationToken);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Error: failed to restore cache - {ex.Message}");
                    return 1;
                }

                bool hasConvLog = !string.IsNullOrEmpty(conversationLogFile);
                bool hasConvDir = !string.IsNullOrEmpty(conversationLogDirectory);
                bool hasPromptInput = promptOption.HasValue() || promptFileOption.HasValue();

                if ((hasConvLog || hasConvDir) && !hasPromptInput)
                {
                    var result = await RunInteractiveAsync(
                        textGenerator,
                        defaultConfig,
                        config,
                        model,
                        system,
                        format,
                        conversationLogFile,
                        conversationLogDirectory,
                        cancellationToken);
                    if (result == 0)
                    {
                        try
                        {
                            await cacheCoordinator.AfterChatAsync(cancellationToken);
                        }
                        catch (Exception ex)
                        {
                            Console.Error.WriteLine($"Error: failed to save cache - {ex.Message}");
                            return 1;
                        }
                    }
                    return result;
                }

                var exitCode = await RunBatchAsync(
                    textGenerator,
                    defaultConfig,
                    config,
                    model,
                    prompt,
                    promptFile,
                    system,
                    systemFile,
                    format,
                    outputFile,
                    outputDirectory,
                    conversationLogFile,
                    conversationLogDirectory,
                    imagePaths,
                    visionOnly,
                    cancellationToken);

                if (exitCode == 0)
                {
                    try
                    {
                        await cacheCoordinator.AfterChatAsync(cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"Error: failed to save cache - {ex.Message}");
                        return 1;
                    }
                }

                return exitCode;
            });

            return await app.ExecuteAsync(args);
        }

        private static async Task<int> RunBatchAsync(
                TextGenerator textGenerator,
                IConfig defaultConfig,
                IConfig config,
                string model,
                string prompt,
                string promptFile,
                string system,
                string systemFile,
            string format,
            string outputFile,
            string outputDirectory,
            string convLogFile,
            string convLogDir,
            IReadOnlyList<string> imagePaths,
            bool visionOnly,
            CancellationToken cancellationToken)
        {
            // Load prompt from file if specified
            if (!string.IsNullOrEmpty(promptFile))
                prompt = await File.ReadAllTextAsync(promptFile, cancellationToken);
            // Load system prompt from file if specified
            if (!string.IsNullOrEmpty(systemFile))
                system = await File.ReadAllTextAsync(systemFile, cancellationToken);

            // Load conversation history
            var conversationLog = await LogManager.LoadConversationAsync(convLogFile, convLogDir, defaultConfig, cancellationToken);

            if (!TryBuildAttachments(imagePaths, out var promptAttachments))
            {
                return 1;
            }

            bool hasPrompt = !string.IsNullOrWhiteSpace(prompt);
            bool hasImages = promptAttachments.Count > 0;

            // Validate inputs
            if (string.IsNullOrEmpty(model))
            {
                Console.WriteLine("Error: Model must be specified.");
                return 1;
            }

            if (!hasPrompt)
            {
                if (!visionOnly)
                {
                    Console.WriteLine("Error: Prompt must be specified unless --vision-only is set.");
                    return 1;
                }

                if (!hasImages)
                {
                    Console.WriteLine("Error: --vision-only requires at least one image.");
                    return 1;
                }
            }

            PromptSequence promptSequence = PromptSequenceBuilder.FromString(prompt, promptAttachments);
            if (!promptSequence.IsValid)
            {
                Console.WriteLine("Error: No prompt content to process.");
                return 1;
            }

            OutputResult outputResult = conversationLog;
            foreach (var message in promptSequence.Messages)
            {
                outputResult = await textGenerator.GenerateTextAsync(
                    model, message.Text, system, config, outputResult, message.Attachments, cancellationToken);
                Console.WriteLine(outputResult.Completion);
            }

            // Output result in the desired format
            string formattedOutput = outputResult.Format(format);
            await LogManager.SaveOutputAsync(formattedOutput, outputFile, outputDirectory, format, cancellationToken);

            return 0;
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
            if (config is LlamaConfig llamaConfig)
            {
                userLabel = llamaConfig.Username;
                assistantLabel = llamaConfig.AssistantName;
            }
            else
            {
                userLabel = "User";
                assistantLabel = "Assistant";
            }

            var interruptTcs = new TaskCompletionSource<bool>();
            Console.CancelKeyPress += (s, e) =>
            {
                // Prevent immediate process termination so we can persist the log, then exit the loop.
                e.Cancel = true;
                interruptTcs.TrySetResult(true);
                Console.WriteLine();
            };

            Console.WriteLine("Conversation mode started (ends with exit/quit, also ends with Ctrl+C)\n");

            foreach (var entry in conversation.History)
            {
                if (!string.IsNullOrEmpty(entry.Prompt))
                {
                    Console.WriteLine($"{userLabel}: {entry.Prompt}");
                }
                if (!string.IsNullOrEmpty(entry.Completion))
                {
                    Console.WriteLine($"{assistantLabel}: {entry.Completion}");
                }
            }
            if (!string.IsNullOrEmpty(conversation.Prompt) || !string.IsNullOrEmpty(conversation.Completion))
            {
                Console.WriteLine($"{userLabel}: {conversation.Prompt}");
                Console.WriteLine($"{assistantLabel}: {conversation.Completion}");
            }
            Console.WriteLine();

            while (!interruptTcs.Task.IsCompleted)
            {
                Console.Write($"{userLabel}> ");
                var inputTask = Task.Run(() => Console.ReadLine());
                var completed = await Task.WhenAny(inputTask, interruptTcs.Task);
                if (completed != inputTask) break;

                string input = inputTask.Result;
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
                        model, input, system, config, conversation, cancellationToken: cancellationToken);
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

                await LogManager.SaveConversationAsync(conversation, convLogFile, convLogDir, format, cancellationToken);
            }

            Console.WriteLine("\nsession ended");

            bool hasConversation =
                conversation.Config != null ||
                conversation.History.Count > 0 ||
                !string.IsNullOrEmpty(conversation.Prompt) ||
                !string.IsNullOrEmpty(conversation.Completion);

            if (hasConversation)
            {
                var savedPath = await LogManager.SaveConversationAsync(conversation, convLogFile, convLogDir, format, cancellationToken);
                if (!string.IsNullOrEmpty(savedPath))
                {
                    Console.WriteLine($"Log saved: {savedPath}");
                }
            }

            return 0;
        }

        private static string ResolveApiHost(out bool usedLegacyHostEnv)
        {
            var primary = Environment.GetEnvironmentVariable("TEXTGEN_API_HOST");
            if (!string.IsNullOrWhiteSpace(primary))
            {
                usedLegacyHostEnv = false;
                return primary;
            }

            var legacy = Environment.GetEnvironmentVariable("OPENAI_API_HOST");
            if (!string.IsNullOrWhiteSpace(legacy))
            {
                usedLegacyHostEnv = true;
                return legacy;
            }

            usedLegacyHostEnv = false;
            return DefaultApiHost;
        }

        private static string ResolveApiKey(string apiHost, ILogger logger)
        {
            if (string.IsNullOrWhiteSpace(apiHost))
                return null;

            var apiEndpoint = apiHost.GetApiEndpoint();
            var isOfficialOpenAiHost = apiHost.IsOfficialOpenAiHost();

            switch (apiEndpoint)
            {
                case ApiEndpoint.Gemini:
                    var geminiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
                    if (string.IsNullOrWhiteSpace(geminiKey))
                    {
                        LogWarning(logger, "GEMINI_API_KEY is not set. Gemini endpoints require GEMINI_API_KEY.");
                    }
                    return geminiKey;

                case ApiEndpoint.OpenAi:
                    if (isOfficialOpenAiHost)
                    {
                        return Environment.GetEnvironmentVariable("OPENAI_API_KEY");
                    }

                    var llamaKeyForOpenAiCompatible = Environment.GetEnvironmentVariable("LLAMA_API_KEY");
                    if (string.IsNullOrWhiteSpace(llamaKeyForOpenAiCompatible))
                    {
                        LogWarning(logger, "LLAMA_API_KEY is not set. Non-OpenAI hosts using OpenAI-compatible endpoints will send requests without an API key.");
                    }
                    return llamaKeyForOpenAiCompatible;

                case ApiEndpoint.Llama:
                    var llamaKey = Environment.GetEnvironmentVariable("LLAMA_API_KEY");
                    if (string.IsNullOrWhiteSpace(llamaKey))
                    {
                        LogWarning(logger, "LLAMA_API_KEY is not set. Requests to llama.cpp hosts will be sent without an API key.");
                    }
                    return llamaKey;

                default:
                    return null;
            }
        }

        private static void LogWarning(ILogger logger, string message)
        {
            logger?.Log($"Warning: {message}");
            if (logger == null)
            {
                Console.Error.WriteLine($"Warning: {message}");
            }
        }

        private static bool TryBuildAttachments(IReadOnlyList<string> imagePaths, out List<Attachment> attachments)
        {
            attachments = new List<Attachment>();
            if (imagePaths == null || imagePaths.Count == 0)
            {
                return true;
            }

            var errors = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var rawPath in imagePaths)
            {
                if (string.IsNullOrWhiteSpace(rawPath))
                    continue;

                var fullPath = Path.GetFullPath(rawPath);
                if (!seen.Add(fullPath))
                {
                    continue;
                }

                if (!File.Exists(fullPath))
                {
                    errors.Add($"Image file not found: {fullPath}");
                    continue;
                }

                if (!ImageAttachment.IsSupportedExtension(fullPath))
                {
                    errors.Add($"Unsupported image file extension: {fullPath}");
                    continue;
                }

                try
                {
                    attachments.Add(ImageAttachment.FromFile(fullPath));
                }
                catch (Exception ex)
                {
                    errors.Add($"Failed to read image '{fullPath}': {ex.Message}");
                }
            }

            if (errors.Count > 0)
            {
                foreach (var error in errors)
                {
                    Console.Error.WriteLine($"Error: {error}");
                }
                return false;
            }

            return true;
        }
    }
}
