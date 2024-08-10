using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace textgen
{
    public class OpenAiConfig : IConfig
    {
        public static readonly OpenAiConfig DefaultConfig = new OpenAiConfig
        {
            MaxTokens = 1200,
            Seed = 0,
            Temperature = 0.7,
            TopP = 1,
            Username = "user",
            AssistantName = "assistant"
        };

        public int MaxTokens { get; set; }
        public int Seed { get; set; }
        public double Temperature { get; set; }
        public double TopP { get; set; }
        public string Username { get; set; }
        public string AssistantName { get; set; }

        public static async Task<IConfig> LoadConfigAsync(string configFile, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(configFile))
            {
                return DefaultConfig; // Use default values
            }

            var configContent = await File.ReadAllTextAsync(configFile, cancellationToken);
            dynamic config = JsonConvert.DeserializeObject(configContent);

            return new OpenAiConfig
            {
                MaxTokens = config.max_tokens ?? DefaultConfig.MaxTokens,
                Seed = config.seed ?? DefaultConfig.Seed,
                Temperature = config.temperature ?? DefaultConfig.Temperature,
                TopP = config.top_p ?? DefaultConfig.TopP,
                Username = config.username ?? DefaultConfig.Username,
                AssistantName = config.assistant_name ?? DefaultConfig.AssistantName
            };
        }

        public string FormatConfig()
        {
            var sb = new StringBuilder();
            sb.Append($"max_tokens={MaxTokens}\n");
            sb.Append($"seed={Seed}\n");
            sb.Append($"temperature={Temperature}\n");
            sb.Append($"top_p={TopP}\n");
            sb.Append($"username={Username}\n");
            sb.Append($"assistant_name={AssistantName}\n");
            return sb.ToString();
        }
    }
}