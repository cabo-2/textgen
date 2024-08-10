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
    public class LlamaTextGenerator : TextGenerator
    {
        public LlamaTextGenerator(HttpClient httpClient, string apiHost) : base(httpClient, apiHost)
        { }

        public override async Task<OutputResult> GenerateTextAsync(string model, string prompt, string system, IConfig conf, OutputResult conversationLog, CancellationToken cancellationToken = default)
        {
            LlamaConfig config = conf as LlamaConfig;

            if (config.Stream)
            {
                Console.Error.WriteLine("Warning: stream is set to true, but it is not supported. Setting to false.");
                config.Stream = false;
            }

            StringBuilder sb = new StringBuilder();
            var history = new List<(string, string)>();

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

                if (!string.IsNullOrEmpty(entry.Prompt) || !string.IsNullOrEmpty(entry.Completion))
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

            return new OutputResult
            {
                Date = DateTime.UtcNow.ToString("o"),
                Host = _apiHost,
                Model = model,
                Config = config,
                System = system,
                History = history,
                Prompt = prompt.Trim(),
                Completion = result.content.ToString().Trim()
            };
        }
    }
}