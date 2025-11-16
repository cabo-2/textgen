# Phase 3 – Provider-specific Vision Updates (Draft)

## Purpose
- Implement the provider wiring for the vision request refactor, using the CLI `--image/-i` entry point and the `GenerationRequest` shape introduced in Phases 1–2.
- Scope covers OpenAI, Gemini, and llama.cpp so the same CLI UX drives multimodal requests across providers.

## Scope & Non-goals
- Scope: CLI flag parsing for images, request validation, provider payload shaping, and output metadata round-tripping.
- Out of scope for this phase: directory-based image discovery, URL uploads/presigned storage, resizing/compression, and UI/GUI integrations.

## CLI Behavior
- New flag: `-i|--image <FILE>` repeatable; images are collected in the order provided. No directory flag support in this phase.
- Validation:
  - Require readable files; reject missing files or unsupported extensions early.
  - Deduplicate by canonical path; keep first occurrence ordering.
  - If images are present with a model that does not advertise vision support, fail with a clear message.
- Request construction:
  - CLI builds `PromptSequence` + `ImageAttachment` metadata (hash/size/MIME) via the existing Phase 1 builder, populating `GenerationRequest`.
  - `--vision-only` (if provided) sends empty prompt text but still creates a `PromptMessage` with attachments.
  - Presence of any image marks the request as “vision” when selecting providers/models (no external model classification).

## Provider Responsibilities
### Cross-provider handling
- Add `SupportsVision` indicator on each provider; `TextGeneratorFactory` must route or reject accordingly when images exist.
- Preserve attachment order and prompt text order when translating to provider payloads.
- Surface per-provider error messages (size limits, MIME) through `OutputResult` logging for debugging.

### OpenAI
- Use existing `/v1/chat/completions` path.
- When attachments exist, build `messages` content parts combining `{"type":"input_text"}` for prompt text and `{"type":"input_image","image_url":{"url":"data:<mime>;base64,<payload>"}}` for each attachment.
- Treat any vision payload as vision-capable; no automatic model detection. If the model is known to be text-only, throw before dispatch.
- Respect `--vision-only` by omitting the text part and sending only image content items.

### Gemini
- Call `models/{model}:generateContent` with `parts` that include `inlineData` for image attachments and text parts for prompts.
- Enforce Gemini’s size limits (e.g., 20MB/file) during validation; aggregate errors before sending.
- If the selected model lacks vision capability, fail fast with guidance (e.g., suggest a `*-vision` model).

### llama.cpp
- Support both `/v1/chat/completions` and `/completion` compatibility flows.
- If images are present, require the server to advertise vision (`--vision` flag on startup or capabilities response). Otherwise, return a clear error.
- Emit base64 `image_url` entries aligned with the existing OpenAI-compatible schema; continue to send plain prompts when no images are attached.

## Output & Logging
- `OutputResult` should record attachment metadata blocks (`@prompt-images`, `@completion-images`, history variants) without storing base64 payloads.
- Include provider-specific warnings (size trimming, rejected files) in debug logs.
- Maintain deterministic `images/` filenames (original basename with collision suffix if needed) as already defined in Phase 1.

## Implementation Checklist
1. CLI: wire `--image/-i` collection into `PromptSequenceBuilder`; add validation errors and help text.
2. Factory: gate dispatch on `SupportsVision`; error when images + non-vision model/provider.
3. OpenAI: add multimodal message parts; ensure `vision-only` skips text segment.
4. Gemini: add `inlineData` parts; enforce file size/MIME constraints preflight.
5. llama.cpp: emit image parts for compatible servers; guard against non-vision setups.
6. Logging/output: persist attachment metadata alongside prompts/completions; confirm round-trip via sample run.

## Open Questions
- Do we need an explicit `--image-format` override in this phase, or defer until URL uploads are supported?
- Should we warn (not fail) when total payload approaches known provider limits (e.g., OpenAI ~20MB)? Draft assumes warn-only.
- How should we pick defaults for “vision-capable” model names when the user passes a bare provider alias?
