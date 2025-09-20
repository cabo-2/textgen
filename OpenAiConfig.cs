using System.Text;
using Newtonsoft.Json;

namespace Textgen
{
    public class OpenAiConfig : IConfig
    {
        public static OpenAiConfig Create() => new OpenAiConfig
        {
            MaxTokens = 1200,
            Seed = 0,
            Temperature = 0.7,
            TopP = 1,
            Stream = true
        };

        public int MaxTokens { get; set; }
        public int Seed { get; set; }
        public double Temperature { get; set; }
        public double TopP { get; set; }
        public bool Stream { get; set; }

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
                var value = Create();
                dynamic config = JsonConvert.DeserializeObject(configJson);

                return new OpenAiConfig
                {
                    MaxTokens = config.max_tokens ?? value.MaxTokens,
                    Seed = config.seed ?? value.Seed,
                    Temperature = config.temperature ?? value.Temperature,
                    TopP = config.top_p ?? value.TopP,
                    Stream = config.stream ?? value.Stream
                };
            }
        }

        public string FormatConfig()
        {
            var sb = new StringBuilder();
            sb.Append($"max_tokens={MaxTokens}\n");
            sb.Append($"seed={Seed}\n");
            sb.Append($"temperature={Temperature}\n");
            sb.Append($"top_p={TopP}\n");
            sb.Append($"stream={Stream.ToString().ToLower()}\n");
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
                        case "max_tokens": config.MaxTokens = int.Parse(value); break;
                        case "seed": config.Seed = int.Parse(value); break;
                        case "temperature": config.Temperature = double.Parse(value); break;
                        case "top_p": config.TopP = double.Parse(value); break;
                        case "stream": config.Stream = bool.Parse(value); break;
                        case "username":
                        case "assistant_name":
                            // Ignore legacy keys to remain backward compatible.
                            break;
                    }
                }
            }

            return config;
        }
    }
}
