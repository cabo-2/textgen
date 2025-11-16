#!/usr/bin/env bash
set -euo pipefail

require_env() {
  local name="$1"
  if [[ -z "${!name:-}" ]]; then
    echo "Error: $name is not set. Export it before running this script." >&2
    exit 1
  fi
}

require_env GEMINI_API_KEY

API_HOST="${TEXTGEN_API_HOST:-https://generativelanguage.googleapis.com/v1beta/models}"
OUTPUT_ROOT="${OUTPUT_ROOT:-tests/runs}"
RUN_ID="$(date +%Y%m%d-%H%M%S)"
SCENARIO="gemini-text"
OUT_DIR="${OUTPUT_ROOT}/${RUN_ID}-${SCENARIO}/output"
PROMPT="tests/assets/prompts/text/sample-quickcheck.txt"

mkdir -p "$OUT_DIR"

TEXTGEN_API_HOST="$API_HOST" dotnet run --project cli -- \
  -m gemini-1.5-flash \
  -P "$PROMPT" \
  -O "$OUT_DIR" \
  "$@"
