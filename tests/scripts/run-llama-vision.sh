#!/usr/bin/env bash
set -euo pipefail

# LLAMA_API_KEY is optional; export it if your server requires one.

usage() {
  echo "Usage: $(basename "$0") <image-path> [extra dotnet args]" >&2
  exit 1
}

[[ $# -ge 1 ]] || usage

IMAGE="$1"
shift

if [[ ! -f "$IMAGE" ]]; then
  echo "Error: image not found: $IMAGE" >&2
  exit 1
fi

API_HOST="${TEXTGEN_API_HOST:-http://localhost:8081/v1/chat/completions}"
OUTPUT_ROOT="${OUTPUT_ROOT:-tests/runs}"
RUN_ID="$(date +%Y%m%d-%H%M%S)"
SCENARIO="llama-vision"
OUT_DIR="${OUTPUT_ROOT}/${RUN_ID}-${SCENARIO}/output"
PROMPT="tests/assets/prompts/vision/sample-vision-check.txt"

mkdir -p "$OUT_DIR"

TEXTGEN_API_HOST="$API_HOST" dotnet run --project cli -- \
  -m local-vision \
  -P "$PROMPT" \
  --image "$IMAGE" \
  -O "$OUT_DIR" \
  "$@"
