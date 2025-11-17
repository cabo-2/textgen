#!/usr/bin/env bash
set -euo pipefail

# Chat Completions interactive session against OpenAI-compatible host.

API_HOST="${TEXTGEN_API_HOST:-http://127.0.0.1:8081/v1/chat/completions}"
OUTPUT_ROOT="${OUTPUT_ROOT:-tests/runs}"
RUN_ID="$(date +%Y%m%d-%H%M%S)"
SCENARIO="openai-interactive"
RUN_DIR="${OUTPUT_ROOT}/${RUN_ID}-${SCENARIO}"
LOG_DIR="${RUN_DIR}/logs"

mkdir -p "$LOG_DIR"

echo "Starting interactive chat against: $API_HOST"
echo "Logs will be saved under: $LOG_DIR"
echo "Exit with Ctrl+C or by typing exit/quit."

TEXTGEN_API_HOST="$API_HOST" dotnet run --project cli -- \
  -m gemma-27b \
  -L "$LOG_DIR" \
  "$@"
