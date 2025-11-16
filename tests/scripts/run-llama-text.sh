#!/usr/bin/env bash
set -euo pipefail

# LLAMA_API_KEY is optional; export it if your server requires one.

API_HOST="${TEXTGEN_API_HOST:-http://localhost:8081/v1/chat/completions}"
OUTPUT_ROOT="${OUTPUT_ROOT:-tests/runs}"
RUN_ID="$(date +%Y%m%d-%H%M%S)"
SCENARIO="llama-text"
OUT_DIR="${OUTPUT_ROOT}/${RUN_ID}-${SCENARIO}/output"
PROMPT="tests/assets/prompts/text/sample-quickcheck.txt"

mkdir -p "$OUT_DIR"

TEXTGEN_API_HOST="$API_HOST" dotnet run --project cli -- \
  -m local-llama \
  -P "$PROMPT" \
  -O "$OUT_DIR" \
  "$@"
