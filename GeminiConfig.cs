using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Textgen
{
    public class GeminiConfig : IConfig
    {
        public static GeminiConfig Create() => new GeminiConfig
        {
            Temperature = 0.9,
            TopP = 1,
            TopK = 1,
            MaxOutputTokens = 2048,
            CandidateCount = 1,
            StopSequences = new List<string>(),
            SafetySettings = null
        };

        public double Temperature { get; set; }
        public double TopP { get; set; }
        public int TopK { get; set; }
        public int MaxOutputTokens { get; set; }
        public int CandidateCount { get; set; }
        public List<string> StopSequences { get; set; }
        public Dictionary<string, string> SafetySettings { get; set; }

        public async Task<IConfig> LoadConfigAsync(string configFile, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(configFile))
            {
                return Create(); // Use default values
            }

            if (Path.GetExtension(configFile) != ".json")
            {
                string configText = await File.ReadAllTextAsync(configFile, cancellationToken);
                return LoadFromText(configText);
            }
            else
            {
                var configJson = await File.ReadAllTextAsync(configFile, cancellationToken);
                return LoadFromJson(configJson);
            }
        }

        public string FormatConfig()
        {
            var sb = new StringBuilder();
            sb.Append($"temperature={Temperature}\n");
            sb.Append($"top_p={TopP}\n");
            sb.Append($"top_k={TopK}\n");
            sb.Append($"max_output_tokens={MaxOutputTokens}\n");
            sb.Append($"candidate_count={CandidateCount}\n");
            if (StopSequences != null && StopSequences.Any())
            {
                sb.Append($"stop_sequences={string.Join(",", StopSequences)}\n");
            }
            if (SafetySettings != null && SafetySettings.Any())
            {
                foreach (var setting in SafetySettings)
                {
                    sb.Append($"{setting.Key}={setting.Value}\n");
                }
            }
            return sb.ToString();
        }

        public IConfig LoadFromText(string textContent)
        {
            var config = Create();
            var lines = textContent.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

            foreach (var line in lines)
            {
                var configLine = line.Split(new[] { '=' }, 2);
                if (configLine.Length == 2)
                {
                    var key = configLine[0].Trim();
                    var value = configLine[1].Trim();

                    switch (key)
                    {
                        case "temperature": config.Temperature = double.Parse(value); break;
                        case "top_p": config.TopP = double.Parse(value); break;
                        case "top_k": config.TopK = int.Parse(value); break;
                        case "max_output_tokens": config.MaxOutputTokens = int.Parse(value); break;
                        case "candidate_count": config.CandidateCount = int.Parse(value); break;
                        case "stop_sequences": config.StopSequences = new List<string>(value.Split(',')); break;
                        default:
                            if (key.StartsWith("HARM_CATEGORY_"))
                            {
                                if (config.SafetySettings == null)
                                {
                                    config.SafetySettings = new Dictionary<string, string>();
                                }
                                config.SafetySettings[key] = value;
                            }
                            break;
                    }
                }
            }

            return config;
        }

        private IConfig LoadFromJson(string configJson)
        {
            var config = Create();
            JObject json = JObject.Parse(configJson);

            config.Temperature = (double?)json["temperature"] ?? config.Temperature;
            config.TopP = (double?)json["top_p"] ?? config.TopP;
            config.TopK = (int?)json["top_k"] ?? config.TopK;
            config.MaxOutputTokens = (int?)json["max_output_tokens"] ?? config.MaxOutputTokens;
            config.CandidateCount = (int?)json["candidate_count"] ?? config.CandidateCount;

            if (json["stop_sequences"] != null)
            {
                config.StopSequences = json["stop_sequences"].ToObject<List<string>>();
            }

            if (json["safety_settings"] != null)
            {
                config.SafetySettings = new Dictionary<string, string>();
                foreach (var setting in json["safety_settings"])
                {
                    string category = setting["category"].ToString();
                    string threshold = setting["threshold"].ToString();
                    config.SafetySettings[category] = threshold;
                }
            }

            return config;
        }
    }
}
