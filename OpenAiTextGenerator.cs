#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace Textgen
{
    public class OpenAiTextGenerator : TextGenerator
    {
        private const string UserRole = "user";
        private const string AssistantRole = "assistant";

        public OpenAiTextGenerator(HttpClient httpClient, string apiHost, ILogger? logger = null) : base(httpClient, apiHost, logger)
        { }

        public override async Task<OutputResult> GenerateTextAsync(string model, string prompt, string system, IConfig conf, OutputResult conversationLog, IReadOnlyList<Attachment>? promptAttachments = null, CancellationToken cancellationToken = default)
        {
            if (conf is not OpenAiConfig config)
                throw new ArgumentException("Expected OpenAiConfig", nameof(conf));

            var history = new List<ConversationTurn>();
            var promptAttachmentList = promptAttachments?.ToList() ?? new List<Attachment>();
            var imageAttachments = promptAttachmentList.OfType<ImageAttachment>().ToList();
            var unsupported = promptAttachmentList.Where(a => a is not ImageAttachment).ToList();
            if (unsupported.Count > 0)
            {
                throw new NotSupportedException($"OpenAI chat completions only support image attachments; found unsupported types: {string.Join(", ", unsupported.Select(a => a.Type))}");
            }
            // Add system to messages
            var messages = new List<dynamic>();
            if (!string.IsNullOrEmpty(system))
            {
                messages.Add(new { role = "system", content = system });
            }

            // Add conversation history to messages
            foreach (var entry in conversationLog.History)
            {
                if (!string.IsNullOrEmpty(entry.Prompt))
                    messages.Add(new { role = UserRole, content = entry.Prompt });

                if (!string.IsNullOrEmpty(entry.Completion))
                    messages.Add(new { role = AssistantRole, content = entry.Completion });

                if (entry.HasContent)
                    history.Add(entry.Clone());
            }

            if (!string.IsNullOrEmpty(conversationLog.Prompt))
                messages.Add(new { role = UserRole, content = conversationLog.Prompt });

            if (!string.IsNullOrEmpty(conversationLog.Completion))
                messages.Add(new { role = AssistantRole, content = conversationLog.Completion });

            if (!string.IsNullOrEmpty(conversationLog.Prompt) ||
                !string.IsNullOrEmpty(conversationLog.Completion) ||
                conversationLog.PromptAttachments.Count > 0 ||
                conversationLog.CompletionAttachments.Count > 0)
            {
                history.Add(new ConversationTurn
                {
                    Prompt = conversationLog.Prompt,
                    Completion = conversationLog.Completion,
                    PromptAttachments = conversationLog.PromptAttachments.Select(p => p.Clone()).ToList(),
                    CompletionAttachments = conversationLog.CompletionAttachments.Select(p => p.Clone()).ToList()
                });
            }

            var userContent = await BuildContentPartsAsync(prompt, imageAttachments, cancellationToken);
            messages.Add(new { role = UserRole, content = userContent }); // current user prompt

            var requestBody = new
            {
                model = model,
                messages = messages,
                max_tokens = config.MaxTokens,
                seed = config.Seed,
                temperature = config.Temperature,
                top_p = config.TopP,
                stream = config.Stream // Include stream configuration
            };

            var jsonRequest = JsonConvert.SerializeObject(requestBody);
            var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(_apiHost, content, cancellationToken);
            response.EnsureSuccessStatusCode();

            if (config.Stream)
            {
                return await ReadStreamedResponseAsync(response, config, system, model, prompt, imageAttachments, history);
            }
            else
            {
                var jsonResponse = await response.Content.ReadAsStringAsync(cancellationToken);
                dynamic result = JsonConvert.DeserializeObject(jsonResponse) ?? throw new InvalidOperationException("Empty response from OpenAI endpoint.");
                var completion = (string?)result?.choices?[0]?.message?.content ?? string.Empty;

                return new OutputResult
                {
                    Date = DateTime.UtcNow.ToString("o"),
                    Host = _apiHost,
                    Model = model,
                    Config = config,
                    System = system,
                    History = history,
                    Prompt = (prompt ?? string.Empty).Trim(),
                PromptAttachments = imageAttachments.Select(p => p.Clone()).ToList(),
                Completion = completion.Trim()
            };
        }
        }

        private async Task<OutputResult> ReadStreamedResponseAsync(HttpResponseMessage response, OpenAiConfig config, string system, string model, string prompt, IReadOnlyList<ImageAttachment> promptImages, List<ConversationTurn> history)
        {
            using (var stream = await response.Content.ReadAsStreamAsync())
            using (var reader = new StreamReader(stream))
            {
                var completionText = new StringBuilder();
                string? line;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    if (line.StartsWith("data:"))
                    {
                        _logger?.Log($"STREAM {line}");
                        line = line.Substring("data:".Length).Trim();

                        if (!string.IsNullOrWhiteSpace(line) && line != "[DONE]")
                        {
                            dynamic? result = JsonConvert.DeserializeObject(line);
                            if (result == null)
                            {
                                continue;
                            }

                            string? content = null;
                            try
                            {
                                content = result?.choices?[0]?.delta?.content;
                            }
                            catch (ArgumentOutOfRangeException)
                            {
                                content = null;
                            }

                            if (!string.IsNullOrEmpty(content))
                            {
                                completionText.Append(content);
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
                    PromptAttachments = promptImages.Select(p => p.Clone()).ToList(),
                    Completion = completionText.ToString().Trim()
                };
            }
        }

        public override IConfig CreateDefaultConfig() => OpenAiConfig.Create();

        private static async Task<object> BuildContentPartsAsync(string prompt, IReadOnlyList<ImageAttachment> promptImages, CancellationToken cancellationToken)
        {
            if (promptImages == null || promptImages.Count == 0)
            {
                return prompt;
            }

            var contentParts = new List<object>();
            if (!string.IsNullOrWhiteSpace(prompt))
            {
                contentParts.Add(new { type = "text", text = prompt });
            }

            foreach (var attachment in promptImages)
            {
                var path = ResolveAttachmentPath(attachment);
                var bytes = await File.ReadAllBytesAsync(path, cancellationToken);
                var base64 = Convert.ToBase64String(bytes);
                var mime = string.IsNullOrWhiteSpace(attachment.MimeType) ? "application/octet-stream" : attachment.MimeType;
                contentParts.Add(new
                {
                    type = "image_url",
                    image_url = new
                    {
                        url = $"data:{mime};base64,{base64}"
                    }
                });
            }

            return contentParts;
        }

        private static string ResolveAttachmentPath(Attachment attachment)
        {
            if (!string.IsNullOrEmpty(attachment.SourcePath) && File.Exists(attachment.SourcePath))
            {
                return attachment.SourcePath;
            }

            if (!string.IsNullOrEmpty(attachment.StoredPath) && File.Exists(attachment.StoredPath))
            {
                return attachment.StoredPath;
            }

            var fallback = attachment.FileName ?? attachment.SourcePath ?? attachment.StoredPath ?? "<unknown>";
            throw new FileNotFoundException($"Attachment file not found: {fallback}");
        }
    }
}
