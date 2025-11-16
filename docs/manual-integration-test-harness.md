# Manual Integration Test Harness (Text + Vision)

## Purpose
- Lightweight, human-driven smoke tests that exercise real provider calls (OpenAI, Gemini, llama.cpp) for both text-only and multimodal requests.
- Reusable workspace under `tests/` so new features can add scenarios without building a full automation harness.
- Make it easy to capture what was run, the raw outputs, and quick visual verification notes.

## Directory Layout (new `tests/` tree)
```
tests/
  README.md                 # entry point / quick reminders
  .gitignore                # keeps runs, images, env files out of git
  assets/
    prompts/
      text/                 # reusable plain text prompts
      vision/               # prompts that reference images
    images/                 # drop local test images here (gitignored)
  runs/                     # create per-run folders: YYYYMMDD-provider-scenario
    ...                     # output files, debug logs, notes.md per run
```
- Baseline prompt seeds: `tests/assets/prompts/text/sample-quickcheck.txt`, `tests/assets/prompts/vision/sample-vision-check.txt`.
- Bring your own images (UI screenshot + natural photo + document scan works well) into `tests/assets/images/`; they stay local.
- Keep each manual session under `tests/runs/<date>-<provider>-<text|vision>/` with `output/` and a short `notes.md` (copy + fill the template below).

## Prerequisites and Settings
- `.NET SDK` installed and `dotnet` on PATH.
- Environment variables (never commit them). Export them once in your shell before running commands; do not inline secrets in the command examples below:
  - `TEXTGEN_API_HOST` selects the API base.
  - `OPENAI_API_KEY`, `GEMINI_API_KEY`, `LLAMA_API_KEY` picked automatically by host (see README).
- Optional run metadata to record in `notes.md`:
  - Model name, host URL, prompt file used, image filename(s), config overrides (e.g., `temperature`), and whether streaming was used.
  - For vision: image type (screenshot/photo/doc), approx. size, and any captions supplied.

### Notes template (drop into each `tests/runs/.../notes.md`)
```
# Run notes
- Scenario: {provider} {text|vision}
- When: $(date -Iseconds)
- Host: ${TEXTGEN_API_HOST}
- Model: ...
- Prompt file: ...
- Images: ...
- Command: ...
- Observations: output quality, any errors/warnings, latency, token usage, whether @prompt-images/@completion sections looked correct.
```

## Common Run Pattern
1. Set env vars for the target provider (examples below) and ensure a writeable `tests/runs/.../` directory exists.
2. Pick a prompt file from `tests/assets/prompts/...` and optional images from `tests/assets/images/`.
3. Run via `dotnet run --project cli -- ...` (add `--debug tests/runs/.../debug.log` if you want HTTP traces).
4. Verify output in-place: formatting (`@prompt`, `@prompt-images`, `@completion-images`), response quality, and any provider warnings.
5. Jot notes in `tests/runs/.../notes.md` and keep the raw output for comparison next time.

## Helper Scripts (`tests/scripts/*.sh`)
- Naming rule: `run-<provider>-<mode>.sh` (provider = `openai|gemini|llama`, mode = `text|vision`).
- Text scripts run with the baked-in prompt files; vision scripts require the first arg to be an image path.
- `OUTPUT_ROOT` can override the default `tests/runs` target. Additional CLI args are forwarded.
- API keys must be exported beforehand; scripts only set `TEXTGEN_API_HOST` (override with your own if needed).
- Examples: `tests/scripts/run-openai-text.sh`, `tests/scripts/run-openai-vision.sh <image>`, `tests/scripts/run-llama-text.sh`.

## Scenario Checklists and Example Commands
- Use `-O tests/runs/<date>-<scenario>/output` to keep artifacts grouped. Replace model names with whatever you need to validate.
- Commands assume chat/completions endpoints; adjust host and model if you target other compatibility layers.

### OpenAI – text
- Env: `TEXTGEN_API_HOST=https://api.openai.com/v1/chat/completions`, `OPENAI_API_KEY` already exported.
- Command:
  ```
  TEXTGEN_API_HOST=https://api.openai.com/v1/chat/completions \
  dotnet run --project cli -- \
    -m gpt-4o-mini \
    -P tests/assets/prompts/text/sample-quickcheck.txt \
    -O tests/runs/$(date +%Y%m%d)-openai-text/output
  ```
- Verify: fluent answer <60 words, sensible structure, no missing `@prompt` block, usage/headers present in JSON output if selected.

### OpenAI – vision
- Env: same host/key exported; choose a small PNG/JPG in `tests/assets/images/`.
- Command:
  ```
  TEXTGEN_API_HOST=https://api.openai.com/v1/chat/completions \
  dotnet run --project cli -- \
    -m gpt-4o \
    -P tests/assets/prompts/vision/sample-vision-check.txt \
    --image tests/assets/images/<your-image>.png \
    -O tests/runs/$(date +%Y%m%d)-openai-vision/output
  ```
- Verify: `@prompt-images` lists the file with MIME/size, OpenAI does not reject content type/size, completion references visual details reasonably, and `vision-only` flag works if you omit text.

### Gemini – text
- Env: `TEXTGEN_API_HOST=https://generativelanguage.googleapis.com/v1beta/models`, `GEMINI_API_KEY` already exported.
- Command:
  ```
  TEXTGEN_API_HOST=https://generativelanguage.googleapis.com/v1beta/models \
  dotnet run --project cli -- \
    -m gemini-1.5-flash \
    -P tests/assets/prompts/text/sample-quickcheck.txt \
    -O tests/runs/$(date +%Y%m%d)-gemini-text/output
  ```
- Verify: coherent short answer, no safety-blocked response, request not rejected for missing model prefix/path, usage/headers show up if available.

### Gemini – vision
- Env: same host/key exported; pick a vision-capable model (`gemini-1.5-pro` or newer) and an image.
- Command:
  ```
  TEXTGEN_API_HOST=https://generativelanguage.googleapis.com/v1beta/models \
  dotnet run --project cli -- \
    -m gemini-1.5-pro \
    -P tests/assets/prompts/vision/sample-vision-check.txt \
    --image tests/assets/images/<your-image>.png \
    -O tests/runs/$(date +%Y%m%d)-gemini-vision/output
  ```
- Verify: `@prompt-images` present, no 400 on file size (>20MB fails), response references visual content, safety filters noted when triggered.

### llama.cpp (OpenAI-compatible) – text
- Env: point to your local llama.cpp server, e.g. `TEXTGEN_API_HOST=http://localhost:8081/v1/chat/completions`; export `LLAMA_API_KEY` if needed (often blank).
- Command:
  ```
  TEXTGEN_API_HOST=http://localhost:8081/v1/chat/completions \
  dotnet run --project cli -- \
    -m local-llama \
    -P tests/assets/prompts/text/sample-quickcheck.txt \
    -O tests/runs/$(date +%Y%m%d)-llama-text/output
  ```
- Verify: server accepts chat payload, no mismatch between requested/loaded model, streaming works if enabled.

### llama.cpp – vision
- Requires the server started with `--vision` (or equivalent) and a model that supports images.
- Command:
  ```
  TEXTGEN_API_HOST=http://localhost:8081/v1/chat/completions \
  dotnet run --project cli -- \
    -m local-vision \
    -P tests/assets/prompts/vision/sample-vision-check.txt \
    --image tests/assets/images/<your-image>.png \
    -O tests/runs/$(date +%Y%m%d)-llama-vision/output
  ```
- Verify: server does not reject the request as text-only, attachments are echoed in output metadata, completion mentions image-specific details. If the server lacks vision, confirm the CLI surfaces a clear, actionable error.

## Adding New Scenarios
- Add a prompt file under `tests/assets/prompts/{text|vision}/` and note the model/host pairing.
- Update this doc with the command + verification bullets; keep commands copy/pasteable.
- If a new provider or feature (e.g., tool-calling) lands, mirror the same pattern: env vars, command, manual checks, and what success/failure looks like.
