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
    public class OpenAiTextGenerator : TextGenerator
    {
        public OpenAiTextGenerator(HttpClient httpClient, string apiHost, ILogger logger = null) : base(httpClient, apiHost, logger)
        { }

        public override async Task<OutputResult> GenerateTextAsync(string model, string prompt, string system, IConfig conf, OutputResult conversationLog, CancellationToken cancellationToken = default)
        {
            OpenAiConfig config = (OpenAiConfig)conf;

            var history = new List<(string, string)>();
            // Add system to messages
            var messages = new List<dynamic>
            {
                new { role = "system", content = system ?? "" }
            };

            // Add conversation history to messages
            foreach (var entry in conversationLog.History)
            {
                if (!string.IsNullOrEmpty(entry.Prompt))
                    messages.Add(new { role = config.Username, content = entry.Prompt });

                if (!string.IsNullOrEmpty(entry.Completion))
                    messages.Add(new { role = config.AssistantName, content = entry.Completion });

                if (!string.IsNullOrEmpty(entry.Prompt) || !string.IsNullOrEmpty(entry.Completion))
                    history.Add((entry.Prompt ?? "", entry.Completion ?? ""));
            }

            if (!string.IsNullOrEmpty(conversationLog.Prompt))
                messages.Add(new { role = config.Username, content = conversationLog.Prompt });

            if (!string.IsNullOrEmpty(conversationLog.Completion))
                messages.Add(new { role = config.AssistantName, content = conversationLog.Completion });

            if (!string.IsNullOrEmpty(conversationLog.Prompt) || !string.IsNullOrEmpty(conversationLog.Completion))
                history.Add((conversationLog.Prompt ?? "", conversationLog.Completion ?? ""));

            messages.Add(new { role = config.Username, content = prompt }); // current user prompt      

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
                return await ReadStreamedResponseAsync(response, config, system, model, prompt, history);
            }
            else
            {
                var jsonResponse = await response.Content.ReadAsStringAsync(cancellationToken);
                dynamic result = JsonConvert.DeserializeObject(jsonResponse);

                return new OutputResult
                {
                    Date = DateTime.UtcNow.ToString("o"),
                    Host = _apiHost,
                    Model = model,
                    Config = config,
                    System = system,
                    History = history,
                    Prompt = prompt.Trim(),
                    Completion = result.choices[0].message.content.ToString().Trim()
                };
            }
        }

        private async Task<OutputResult> ReadStreamedResponseAsync(HttpResponseMessage response, OpenAiConfig config, string system, string model, string prompt, List<(string, string)> history)
        {
            using (var stream = await response.Content.ReadAsStreamAsync())
            using (var reader = new StreamReader(stream))
            {
                var completionText = new StringBuilder();
                string line;
                while ((line = await reader.ReadLineAsync()) != null)
                {                    
                    if (line.StartsWith("data:"))
                    {
                        _logger?.Log($"STREAM {line}");                        
                        line = line.Substring("data:".Length).Trim();

                        if (!string.IsNullOrWhiteSpace(line) && line != "[DONE]")
                        {
                            dynamic result = JsonConvert.DeserializeObject(line);
                            string content = result.choices[0].delta?.content;
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
                    Prompt = prompt.Trim(),
                    Completion = completionText.ToString().Trim()
                };
            }
        }

        public override IConfig CreateDefaultConfig() => OpenAiConfig.Create();
    }
}