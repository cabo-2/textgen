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
    public abstract class TextGenerator
    {
        protected readonly HttpClient _httpClient;
        protected readonly string _apiHost;

        public TextGenerator(HttpClient httpClient, string apiHost)
        {
            _httpClient = httpClient;
            _apiHost = apiHost;
        }

        public abstract Task<OutputResult> GenerateTextAsync(string model, string prompt, string system, IConfig conf, OutputResult conversationLog, CancellationToken cancellationToken = default);

        public abstract IConfig CreateDefaultConfig();
    }
}