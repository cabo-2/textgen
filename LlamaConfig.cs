using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace textgen
{
    public class LlamaConfig : IConfig
    {
        public static LlamaConfig Create() => new LlamaConfig
        {
            NPredict = 1200,
            Seed = 1337,
            Temperature = 0.7,
            TopK = 50,
            TopP = 0.9,
            MinP = 0.1,
            PresencePenalty = 0.0,
            FrequencyPenalty = 0.0,
            RepeatPenalty = 1.1,
            Stream = false,
            CachePrompt = true,
            Username = "user",
            AssistantName = "assistant"
        };

        public int NPredict { get; set; }
        public int Seed { get; set; }
        public double Temperature { get; set; }
        public int TopK { get; set; }
        public double TopP { get; set; }
        public double MinP { get; set; }
        public double PresencePenalty { get; set; }
        public double FrequencyPenalty { get; set; }
        public double RepeatPenalty { get; set; }
        public bool Stream { get; set; }
        public bool CachePrompt { get; set; }
        public string Username { get; set; }
        public string AssistantName { get; set; }

        public async Task<IConfig> LoadConfigAsync(string configFile, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(configFile))
            {
                return Create(); // Use default values
            }

            var configContent = await File.ReadAllTextAsync(configFile, cancellationToken);
            var value = Create();
            dynamic config = JsonConvert.DeserializeObject(configContent);

            return new LlamaConfig
            {
                NPredict = config.n_predict ?? value.NPredict,
                Seed = config.seed ?? value.Seed,
                Temperature = config.temperature ?? value.Temperature,
                TopK = config.top_k ?? value.TopK,
                TopP = config.top_p ?? value.TopP,
                MinP = config.min_p ?? value.MinP,
                PresencePenalty = config.presence_penalty ?? value.PresencePenalty,
                FrequencyPenalty = config.frequency_penalty ?? value.FrequencyPenalty,
                RepeatPenalty = config.repeat_penalty ?? value.RepeatPenalty,
                Stream = config.stream ?? value.Stream,
                CachePrompt = config.cache_prompt ?? value.CachePrompt,
                Username = config.username ?? value.Username,
                AssistantName = config.assistant_name ?? value.AssistantName
            };
        }

        public string FormatConfig()
        {
            var sb = new StringBuilder();
            sb.Append($"n_predict={NPredict}\n");
            sb.Append($"seed={Seed}\n");
            sb.Append($"temperature={Temperature}\n");
            sb.Append($"top_k={TopK}\n");
            sb.Append($"top_p={TopP}\n");
            sb.Append($"min_p={MinP}\n");
            sb.Append($"presence_penalty={PresencePenalty}\n");
            sb.Append($"frequency_penalty={FrequencyPenalty}\n");
            sb.Append($"repeat_penalty={RepeatPenalty}\n");
            sb.Append($"stream={Stream.ToString().ToLower()}\n");
            sb.Append($"cache_prompt={CachePrompt.ToString().ToLower()}\n");
            sb.Append($"username={Username}\n");
            sb.Append($"assistant_name={AssistantName}\n");
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

            return config;
        }
    }
}