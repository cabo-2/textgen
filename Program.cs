﻿using System;
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
            var promptFileOption = app.Option("-P|--prompt-file <FNAME>", "Input message from a file.", CommandOptionType.SingleValue);
            var systemOption = app.Option("-s|--system <SYSTEM_PROMPT>", "System prompt directly.", CommandOptionType.SingleValue);
            var systemFileOption = app.Option("-S|--system-file <FNAME>", "System prompt from a file.", CommandOptionType.SingleValue);
            var formatOption = app.Option("-f|--format <FORMAT>", "Output format (text, json)", CommandOptionType.SingleValue);
            formatOption.DefaultValue = "text";
            formatOption.Accepts().Values("text", "json");
            var outputOption = app.Option("-o|--output <FILE_PATH>", "Output file path (default is standard output).", CommandOptionType.SingleValue);
            var outputDirectoryOption = app.Option("-O|--output-dir <DIR_PATH>", "Directory to save output file.", CommandOptionType.SingleValue);
            var configOption = app.Option("-c|--config <FNAME>", "Parameter settings file (JSON).", CommandOptionType.SingleValue);
            configOption.Accepts().ExistingFile();
            var conversationLogOption = app.Option("-l|--conversation-log <FNAME>", "File to read and maintain conversation logs.", CommandOptionType.SingleValue);
            conversationLogOption.Accepts().ExistingFile();
            var conversationLogDirectoryOption = app.Option("-L|--conversation-log-dir <DIR_PATH>", "Directory to read conversation logs from.", CommandOptionType.SingleValue);
            conversationLogDirectoryOption.Accepts().ExistingDirectory();

            app.HelpOption("-h|--help");
            app.VersionOption("-v|--version", "1.0.0");

            app.OnExecuteAsync(async (cancellationToken) =>
            {
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
                var textGenerator = TextGeneratorFactory.CreateGenerator(openAIHost, httpClient);
                var defaultConfig = textGenerator.CreateDefaultConfig();

                // Load parameters from config file if specified
                var config = await defaultConfig.LoadConfigAsync(configFile, cancellationToken);

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

                StructuredPrompt structuredPrompt = new StructuredPrompt(prompt);
                OutputResult outputResult;
                if (structuredPrompt.IsValidStructuredPrompt)
                {
                    for (int i = 0; i < structuredPrompt.Prompts.Count - 1; i++)
                    {
                        conversationLog = await textGenerator.GenerateTextAsync(model, structuredPrompt.Prompts[i], system, config, conversationLog, cancellationToken);
                    }                    
                    outputResult = await textGenerator.GenerateTextAsync(model, structuredPrompt.Prompts.Last(), system, config, conversationLog, cancellationToken);
                }
                else
                {
                    // Use the existing logic for plain text prompts
                    outputResult = await textGenerator.GenerateTextAsync(model, prompt, system, config, conversationLog, cancellationToken);
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

                // Always output to console
                Console.WriteLine(outputResult.Completion);

                return 0;
            });

            return await app.ExecuteAsync(args);
        }
    }
}