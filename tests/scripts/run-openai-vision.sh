#!/usr/bin/env bash
set -euo pipefail

require_env() {
  local name="$1"
  if [[ -z "${!name:-}" ]]; then
    echo "Error: $name is not set. Export it before running this script." >&2
    exit 1
  fi
}

usage() {
  echo "Usage: $(basename "$0") <image-path> [extra dotnet args]" >&2
  exit 1
}

require_env OPENAI_API_KEY
[[ $# -ge 1 ]] || usage

IMAGE="$1"
shift

if [[ ! -f "$IMAGE" ]]; then
  echo "Error: image not found: $IMAGE" >&2
  exit 1
fi

API_HOST="${TEXTGEN_API_HOST:-https://api.openai.com/v1/chat/completions}"
OUTPUT_ROOT="${OUTPUT_ROOT:-tests/runs}"
RUN_ID="$(date +%Y%m%d-%H%M%S)"
SCENARIO="openai-vision"
OUT_DIR="${OUTPUT_ROOT}/${RUN_ID}-${SCENARIO}/output"
PROMPT="tests/assets/prompts/vision/sample-vision-check.txt"

mkdir -p "$OUT_DIR"

TEXTGEN_API_HOST="$API_HOST" dotnet run --project cli -- \
  -m gpt-4o \
  -P "$PROMPT" \
  --image "$IMAGE" \
  -O "$OUT_DIR" \
  "$@"
