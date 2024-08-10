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
    public class Config
    {
        public static readonly Config DefaultConfig = new Config
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

        public static async Task<Config> LoadConfigAsync(string configFile, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(configFile))
            {
                return DefaultConfig; // Use default values
            }

            var configContent = await File.ReadAllTextAsync(configFile, cancellationToken);
            dynamic config = JsonConvert.DeserializeObject(configContent);

            return new Config
            {
                NPredict = config.n_predict ?? DefaultConfig.NPredict,
                Seed = config.seed ?? DefaultConfig.Seed,
                Temperature = config.temperature ?? DefaultConfig.Temperature,
                TopK = config.top_k ?? DefaultConfig.TopK,
                TopP = config.top_p ?? DefaultConfig.TopP,
                MinP = config.min_p ?? DefaultConfig.MinP,
                PresencePenalty = config.presence_penalty ?? DefaultConfig.PresencePenalty,
                FrequencyPenalty = config.frequency_penalty ?? DefaultConfig.FrequencyPenalty,
                RepeatPenalty = config.repeat_penalty ?? DefaultConfig.RepeatPenalty,
                Stream = config.stream ?? DefaultConfig.Stream,
                CachePrompt = config.cache_prompt ?? DefaultConfig.CachePrompt,
                Username = config.username ?? DefaultConfig.Username,
                AssistantName = config.assistant_name ?? DefaultConfig.AssistantName
            };
        }
    }
}