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
        public static OpenAiConfig Create() => new OpenAiConfig
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

        public async Task<IConfig> LoadConfigAsync(string configFile, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(configFile))
            {
                return Create(); // Use default values
            }

            var configContent = await File.ReadAllTextAsync(configFile, cancellationToken);
            var value = Create();
            dynamic config = JsonConvert.DeserializeObject(configContent);

            return new OpenAiConfig
            {
                MaxTokens = config.max_tokens ?? value.MaxTokens,
                Seed = config.seed ?? value.Seed,
                Temperature = config.temperature ?? value.Temperature,
                TopP = config.top_p ?? value.TopP,
                Username = config.username ?? value.Username,
                AssistantName = config.assistant_name ?? value.AssistantName
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

        public IConfig LoadFromText(string textContent)
        {
            throw new NotSupportedException();
        }
    }
}