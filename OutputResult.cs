using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace textgen
{
    public class OutputResult
    {
        public string Date { get; set; }
        public string Host { get; set; }
        public string Model { get; set; }
        public IConfig Config { get; set; }
        public string System { get; set; }
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
                    sb.Append(Config.FormatConfig());
                    sb.Append("\n");
                    sb.Append($"@system\n{System}\n\n");

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
                    var config = new LlamaConfig();
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
                else if (line.StartsWith("@system"))
                {
                    result.System = lines[++i];
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
}