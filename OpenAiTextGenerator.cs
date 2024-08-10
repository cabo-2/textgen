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
        public OpenAiTextGenerator(HttpClient httpClient, string apiHost) : base(httpClient, apiHost)
        {}

        public override async Task<OutputResult> GenerateTextAsync(string model, string prompt, string systemPrompt, IConfig conf, OutputResult conversationLog, CancellationToken cancellationToken = default)
        {
            OpenAiConfig config = (OpenAiConfig)conf;

            var history = new List<(string, string)>();
            // Add system-prompt to messages
            var messages = new List<dynamic>
            {
                new { role = "system", content = systemPrompt ?? "" }
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
                top_p = config.TopP
            };

            var jsonRequest = JsonConvert.SerializeObject(requestBody);
            var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(_apiHost, content, cancellationToken);
            response.EnsureSuccessStatusCode();

            var jsonResponse = await response.Content.ReadAsStringAsync(cancellationToken);
            dynamic result = JsonConvert.DeserializeObject(jsonResponse);

            return new OutputResult
            {
                Date = DateTime.UtcNow.ToString("o"),
                Host = _apiHost,
                Model = model,
                Config = config,
                SystemPrompt = systemPrompt,
                History = history,
                Prompt = prompt.Trim(),
                Completion = result.choices[0].message.content.ToString().Trim()
            };
        }
    }
}