# TextGen: llama.cpp (llama-server) and OpenAI Compatible Text Generation Console Client

TextGen is a console-based client for text generation using a server implementing the [llama.cpp (llama-server)](https://github.com/ggerganov/llama.cpp) text generation and an OpenAI Compatible API. It supports interaction with various models, including `gpt-4o` and `gpt-4o-mini`.


## Features

- Connects to an [OpenAI-compatible API](https://platform.openai.com/docs/guides/text-generation) for text generation.
- Allows interaction with different models.
- Supports input through direct prompts or files.
- Provides output in multiple formats: text and JSON.
- Maintains conversation logs for continuity and analysis.
- Configurable via command-line options or a configuration file.


## Requirements

- [.NET SDK](https://dotnet.microsoft.com/download)
- Access to an OpenAI-compatible API or a local LLM llama.cpp server instance.


## Setup

1. **Clone the repository**

```bash
git clone <repository-url>
cd textgen
```

2. **Build the application**

```bash
dotnet build
```

## How to Use

This program offers a variety of command-line options for interacting with the text generation API.


### General Command Structure

```bash
dotnet run -- [options]
Options

    -m|--model <MODEL>: Specify the model (e.g., gpt-4o, gpt-4o-mini).

    -p|--prompt <PROMPT>: Input the prompt directly.

    -P|--prompt-file <FNAME>: Provide prompt from a file.

    -s|--system <SYSTEM_PROMPT>: Provide system prompt directly.

    -S|--system-file <FNAME>: Specify system prompt from a file.

    -f|--format <FORMAT>: Set output format (text, json).

    -o|--output <FILE_PATH>: Specify output file path.

    -O|--output-dir <DIR_PATH>: Set directory to save output.

    -c|--config <FNAME>: Use a configuration file (text, json).

    -l|--conversation-log <FNAME>: Read and maintain conversation logs.

    -L|--conversation-log-dir <DIR_PATH>: Directory to read conversation logs.

    -h|--help: Show help information.

    -v|--version: Display version information.
```

### Set API configurations

Ensure environment variables OPENAI_API_HOST and OPENAI_API_KEY are set.

Example:
```bash
export OPENAI_API_HOST="http://localhost:8081/completion"
export OPENAI_API_KEY="your_api_key"
```

*For llama.cpp-server, API keys are not required.*


### API Endpoints

1. **/completion Endpoint**

Use this endpoint for basic text completion tasks.

Example usage:
```bash
dotnet run -- -m gemma-27b -p "What is the capital of France?" -o ~/output.txt
```

*When using llama.cpp, a single model loaded by llama-server is executed regardless of the specified model name.*
*The "/completions" endpoint behaves similarly to the standard web UI of llama-server. For new models, especially those with chat-template specifications, it is recommended to use "/v1/chat/completions".*


Example config:
```txt
n_predict=1200
seed=1337
temperature=0.7
top_k=50
top_p=0.9
min_p=0.1
presence_penalty=0
frequency_penalty=0
repeat_penalty=1.1
stream=true
cache_prompt=true
username=user
assistant_name=assistant
```


2. **/v1/chat/completions Endpoint**

Utilize this endpoint for chat-based interactions.

Chat models like gpt-4o can be accessed here.

Example usage:
```bash
export OPENAI_API_HOST="https://api.openai.com/v1/chat/completions"
export OPENAI_API_KEY="your_api_key"

dotnet run -- -m gpt-4o-mini -p "Show me a code snippet of a website's sticky header in CSS and JavaScript." -o ~/output.txt
```

**Please handle API keys with utmost care!**


Example config:
```txt
max_tokens=1200
seed=0
temperature=0.7
top_p=1
stream=true
username=user
assistant_name=assistant
```


## Output

The completion results can be saved to a specified file or directory. The application supports both plain text and JSON formats for easy integration and analysis.


## License

MIT License.
