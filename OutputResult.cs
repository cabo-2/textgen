using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Textgen
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
                    return JsonConvert.SerializeObject(this, Formatting.Indented);

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

        public static async Task<OutputResult> LoadFromFileAsync(string filePath, IConfig defaultConfig, CancellationToken cancellationToken)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("File not found", filePath);
            }

            var fileContent = await File.ReadAllTextAsync(filePath, cancellationToken);
            return filePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ? LoadFromJson(fileContent) : LoadFromText(fileContent, defaultConfig);
        }

        private static OutputResult LoadFromJson(string jsonContent)
        {
            return JsonConvert.DeserializeObject<OutputResult>(jsonContent);
        }

        private static OutputResult LoadFromText(string textContent, IConfig defaultConfig)
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
                    StringBuilder configText = new StringBuilder();
                    while (i + 1 < lines.Length && !lines[i + 1].StartsWith("@"))
                    {
                        configText.AppendLine(lines[++i]);
                    }
                    result.Config = defaultConfig.LoadFromText(configText.ToString());
                }
                else if (line.StartsWith("@model"))
                {
                    result.Model = lines[++i];
                }
                else if (line.StartsWith("@system"))
                {
                    StringBuilder systemText = new StringBuilder();
                    while (i + 1 < lines.Length && !lines[i + 1].StartsWith("@"))
                    {
                        systemText.AppendLine(lines[++i]);
                    }
                    result.System = systemText.ToString().Trim();
                }
                else if (line.StartsWith("@history-prompt"))
                {
                    var index = int.Parse(line.Split('_')[1]);
                    StringBuilder promptText = new StringBuilder();
                    while (i + 1 < lines.Length && !lines[i + 1].StartsWith("@"))
                    {
                        promptText.AppendLine(lines[++i]);
                    }
                    prompts[index] = promptText.ToString().Trim();
                }
                else if (line.StartsWith("@history-completion"))
                {
                    var index = int.Parse(line.Split('_')[1]);
                    StringBuilder completionText = new StringBuilder();
                    while (i + 1 < lines.Length && !lines[i + 1].StartsWith("@"))
                    {
                        completionText.AppendLine(lines[++i]);
                    }
                    completions[index] = completionText.ToString().Trim();
                }
                else if (line.StartsWith("@prompt"))
                {
                    StringBuilder promptText = new StringBuilder();
                    while (i + 1 < lines.Length && !lines[i + 1].StartsWith("@"))
                    {
                        promptText.AppendLine(lines[++i]);
                    }
                    result.Prompt = promptText.ToString().Trim();
                }
                else if (line.StartsWith("@completion"))
                {
                    StringBuilder completionText = new StringBuilder();
                    while (i + 1 < lines.Length && !lines[i + 1].StartsWith("@"))
                    {
                        completionText.AppendLine(lines[++i]);
                    }
                    result.Completion = completionText.ToString().Trim();
                }
            }

            // Merge prompts and completions
            var maxIndex = Math.Max(prompts.Count > 0 ? prompts.Keys.Max() : 0, completions.Count > 0 ? completions.Keys.Max() : 0);
            for (int j = 1; j <= maxIndex; j++)
            {
                string prompt = prompts.ContainsKey(j) ? prompts[j] : null;
                string completion = completions.ContainsKey(j) ? completions[j] : null;
                result.History.Add((prompt, completion));
            }

            return result;
        }
    }
}