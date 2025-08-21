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

namespace Textgen
{
    public class GeminiTextGenerator : TextGenerator
    {
        private readonly string _apiKey;

        public GeminiTextGenerator(HttpClient httpClient, string apiHost, string apiKey, ILogger logger = null) : base(httpClient, apiHost, logger)
        {
            _apiKey = apiKey;
        }

        public override async Task<OutputResult> GenerateTextAsync(string model, string prompt, string system, IConfig conf, OutputResult conversationLog, CancellationToken cancellationToken = default)
        {
            GeminiConfig config = (GeminiConfig)conf;

            var history = new List<(string, string)>();
            // Add conversation history to messages
            foreach (var entry in conversationLog.History)
            {
                if (!string.IsNullOrEmpty(entry.Prompt) || !string.IsNullOrEmpty(entry.Completion))
                    history.Add((entry.Prompt ?? "", entry.Completion ?? ""));
            }

            if (!string.IsNullOrEmpty(conversationLog.Prompt) || !string.IsNullOrEmpty(conversationLog.Completion))
                history.Add((conversationLog.Prompt ?? "", conversationLog.Completion ?? ""));

            // Construct the messages for the Gemini API request
            var contents = new List<dynamic>();
            if (!string.IsNullOrEmpty(system))
            {
                contents.Add(new
                {
                    role = "user",
                    parts = new[] { new { text = system } }
                });
                contents.Add(new { role = "model", parts = new[] { new { text = "OK" } } });
            }
            foreach (var entry in history)
            {
                if (!string.IsNullOrEmpty(entry.Item1))
                {
                    contents.Add(new
                    {
                        role = "user",
                        parts = new[] { new { text = entry.Item1 } }
                    });
                }

                if (!string.IsNullOrEmpty(entry.Item2))
                {
                    contents.Add(new
                    {
                        role = "model",
                        parts = new[] { new { text = entry.Item2 } }
                    });
                }
            }

            // Current user prompt
            contents.Add(new
            {
                role = "user",
                parts = new[] { new { text = prompt } }
            });

            var requestBody = new
            {
                contents = contents,
                generationConfig = new
                {
                    temperature = config.Temperature,
                    topP = config.TopP,
                    topK = config.TopK,
                    maxOutputTokens = config.MaxOutputTokens,
                    candidateCount = config.CandidateCount,
                    stopSequences = config.StopSequences
                },
                safetySettings = new List<dynamic>()
            };

            if (config.SafetySettings != null)
            {
                foreach (var setting in config.SafetySettings)
                {
                    requestBody.safetySettings.Add(new
                    {
                        category = setting.Key,
                        threshold = setting.Value
                    });
                }
            }

            var jsonRequest = JsonConvert.SerializeObject(requestBody);
            var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

            var requestUrl = $"{_apiHost}/{model}:generateContent?key={_apiKey}";
            var response = await _httpClient.PostAsync(requestUrl, content, cancellationToken);
            response.EnsureSuccessStatusCode();

            var jsonResponse = await response.Content.ReadAsStringAsync(cancellationToken);
            dynamic result = JsonConvert.DeserializeObject(jsonResponse);

            var completion = result.candidates[0].content.parts[0].text.ToString().Trim();

            return new OutputResult
            {
                Date = DateTime.UtcNow.ToString("o"),
                Host = _apiHost,
                Model = model,
                Config = config,
                System = system,
                History = history,
                Prompt = prompt.Trim(),
                Completion = completion
            };
        }

        public override IConfig CreateDefaultConfig() => GeminiConfig.Create();
    }
}
