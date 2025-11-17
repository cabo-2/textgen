#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace Textgen
{
    public class LlamaTextGenerator : TextGenerator
    {
        public LlamaTextGenerator(HttpClient httpClient, string apiHost, ILogger? logger = null) : base(httpClient, apiHost, logger)
        { }

        public override async Task<OutputResult> GenerateTextAsync(string model, string prompt, string system, IConfig conf, OutputResult conversationLog, IReadOnlyList<ImageAttachment>? promptImages = null, CancellationToken cancellationToken = default)
        {
            if (conf is not LlamaConfig config)
                throw new ArgumentException("Expected LlamaConfig", nameof(conf));

            StringBuilder sb = new StringBuilder();
            var history = new List<ConversationTurn>();
            var promptImageList = promptImages?.ToList() ?? new List<ImageAttachment>();

            // Add system to request prompt
            if (!string.IsNullOrEmpty(system))
                sb.Append($"{system}\n\n");

            // Add conversation history to request prompt
            foreach (var entry in conversationLog.History)
            {
                if (!string.IsNullOrEmpty(entry.Prompt))
                    sb.Append($"{config.Username}: {entry.Prompt}\n\n");

                if (!string.IsNullOrEmpty(entry.Completion))
                    sb.Append($"{config.AssistantName}: {entry.Completion}\n\n");

                if (entry.HasContent)
                    history.Add(entry.Clone());
            }

            if (!string.IsNullOrEmpty(conversationLog.Prompt))
                sb.Append($"{config.Username}: {conversationLog.Prompt}\n\n");

            if (!string.IsNullOrEmpty(conversationLog.Completion))
                sb.Append($"{config.AssistantName}: {conversationLog.Completion}\n\n");

            if (!string.IsNullOrEmpty(conversationLog.Prompt) ||
                !string.IsNullOrEmpty(conversationLog.Completion) ||
                conversationLog.PromptImages.Count > 0 ||
                conversationLog.CompletionImages.Count > 0)
            {
                history.Add(new ConversationTurn
                {
                    Prompt = conversationLog.Prompt,
                    Completion = conversationLog.Completion,
                    PromptImages = conversationLog.PromptImages.Select(p => p.Clone()).ToList(),
                    CompletionImages = conversationLog.CompletionImages.Select(p => p.Clone()).ToList()
                });
            }

            // Add prompt to request prompt
            sb.Append($"{config.Username}: {prompt}\n\n");
            sb.Append($"{config.AssistantName}: ");

            List<string> stop = new();
            if (config.Stop)
            {
                stop.Add("</s>");
                stop.Add($"{config.Username}:");
                stop.Add($"{config.AssistantName}:");
            }

            if (promptImageList.Count > 0)
            {
                _logger?.Log("WARNING: prompt images were provided but llama.cpp /completion does not support vision; images will be ignored and request will be text-only.");
            }

            var requestBody = new
            {
                prompt = sb.ToString(),
                n_predict = config.NPredict,
                seed = config.Seed,
                temperature = config.Temperature,
                stop,
                top_k = config.TopK,
                top_p = config.TopP,
                min_p = config.MinP,
                presence_penalty = config.PresencePenalty,
                frequency_penalty = config.FrequencyPenalty,
                repeat_penalty = config.RepeatPenalty,
                stream = config.Stream,
                cache_prompt = config.CachePrompt,
                slot_id = config.SlotId
            };

            var jsonRequest = JsonConvert.SerializeObject(requestBody);
            var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(_apiHost, content, cancellationToken);
            response.EnsureSuccessStatusCode();

            if (config.Stream)
            {
                return await ReadStreamedResponseAsync(response, config, system, model, prompt, promptImageList, history);
            }
            else
            {
                var jsonResponse = await response.Content.ReadAsStringAsync(cancellationToken);
                dynamic result = JsonConvert.DeserializeObject(jsonResponse) ?? throw new InvalidOperationException("Empty response from llama.cpp endpoint.");
                var completion = (string?)result.content ?? string.Empty;

                return new OutputResult
                {
                    Date = DateTime.UtcNow.ToString("o"),
                    Host = _apiHost,
                    Model = model,
                    Config = config,
                    System = system,
                    History = history,
                    Prompt = (prompt ?? string.Empty).Trim(),
                    PromptImages = promptImageList.Select(p => p.Clone()).ToList(),
                    Completion = completion.Trim()
                };
            }
        }

        private async Task<OutputResult> ReadStreamedResponseAsync(HttpResponseMessage response, LlamaConfig config, string system, string model, string prompt, IReadOnlyList<ImageAttachment> promptImages, List<ConversationTurn> history)
        {
            using (var stream = await response.Content.ReadAsStreamAsync())
            using (var reader = new StreamReader(stream))
            {
                var completionText = new StringBuilder();
                string stopSequence = $"{config.Username}:";
                string? line;

                while ((line = await reader.ReadLineAsync()) != null)
                {
                    if (line.StartsWith("data:"))
                    {
                        _logger?.Log($"STREAM {line}");
                        line = line.Substring("data:".Length).Trim();

                        if (!string.IsNullOrEmpty(line))
                        {
                            dynamic? result = JsonConvert.DeserializeObject(line);
                            if (result == null)
                            {
                                continue;
                            }

                            var content = (string?)result?.content;

                            if (!string.IsNullOrEmpty(content))
                            {
                                completionText.Append(content);

                                // Check if stop sequence is found
                                int stopIndex = completionText.ToString().IndexOf(stopSequence, StringComparison.OrdinalIgnoreCase);
                                if (stopIndex >= 0)
                                {
                                    // If found, remove everything after the stop sequence
                                    completionText.Length = stopIndex;
                                    break;
                                }
                            }
                        }
                    }
                }

                return new OutputResult
                {
                    Date = DateTime.UtcNow.ToString("o"),
                    Host = _apiHost,
                    Model = model,
                    Config = config,
                    System = system,
                    History = history,
                    Prompt = (prompt ?? string.Empty).Trim(),
                    PromptImages = promptImages.Select(p => p.Clone()).ToList(),
                    Completion = completionText.ToString().Trim()
                };
            }
        }

        public override IConfig CreateDefaultConfig() => LlamaConfig.Create();
    }
}
