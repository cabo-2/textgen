# Vision-to-Text Specification

## 1. Purpose and Scope
- Extend Textgen so users can submit images (single or batch) and receive text descriptions/transcriptions using any provider that surfaces multimodal endpoints (OpenAI, Google Gemini, local llama.cpp variants).
- Support both conversational prompts that reference images and pure image-to-text runs (no textual prompt required).
- Maintain parity with current logging/output mechanisms so downstream tooling (JSON/text outputs, conversation logs) keeps working.

## 2. Goals
1. Provide a minimal CLI UX for attaching images to a request without forcing users to hand-edit JSON payloads.
2. Allow model/provider-specific configuration of image encoding and transport (inline base64 vs. remote URL reference).
3. Keep the TextGenerator abstraction provider-agnostic by introducing a `VisionRequest` shape that each provider implementation can translate as needed.
4. Ensure deterministic, reproducible runs via explicit image ordering and metadata captured in logs/output.

## 3. Non-Goals
- Building an image preprocessing pipeline (resizing, OCR, filters). We rely on raw user images.
- Providing GUI workflows. This work targets the CLI only.
- Streaming binary image uploads directly to providers that forbid base64 payloads. We assume the existing HTTP stack can handle base64 payload sizes within provider limits.

## 4. User Stories
1. **Single image caption**: As a user I run `textgen --model gpt-4o-mini --image ./diagram.png` and get a description of the diagram.
2. **Prompt + image**: I provide a textual prompt plus multiple screenshots to guide the assistant.
3. **Batch transcription**: I pass a folder of PNG receipts and request structured JSON output, saving results per-file.
4. **Config-driven run**: I define a YAML/JSON config that pins max image resolution, fallback text prompt, and provider-specific API host.

## 5. CLI Experience
### 5.1 New Arguments
- `-I|--image <PATH>`: Add one image file. Repeatable.
- `--image-dir <DIR>`: Include all images in directory (non-recursive by default; `--recursive` flag already exists? if not, add `--image-recursive`).
- `--image-format <inline|url>`: Force encoding. Default provider-specific.
- `--image-max-pixels <INT>`: Soft limit for warning/logging; actual resizing out-of-scope but value stored for future enforcement.
- `--vision-only`: Skip textual prompt; provider receives only images plus optional system prompt.

Argument precedence: explicit `--image` entries override auto-discovered directories. Duplicate paths resolved once based on canonical path.

### 5.2 Help/Docs
Update README usage table and `--help` output with descriptions and caveats (supported file extensions, max size warnings, multi-provider behavior).

## 6. Supported File Types and Validation
- Accept `jpg`, `jpeg`, `png`, `gif`, `webp`, `bmp`, `tiff`, `heic` (if runtime can load). Validation happens before request build.
- For unsupported extension: fail fast with actionable error.
- Check file existence and readability; aggregate failures before exit.
- Capture per-image metadata (name, size bytes, sha256) for logging to ensure reproducibility.

## 7. Internal Data Model
### 7.1 New Types
```csharp
public record VisionRequest(
    IReadOnlyList<ImageAttachment> Images,
    string? Prompt,
    string? SystemPrompt,
    OutputFormat OutputFormat,
    IReadOnlyDictionary<string, string> ProviderHints);

public record ImageAttachment(
    string CanonicalPath,
    string OriginalArgument,
    string MimeType,
    long SizeBytes,
    string Base64Data);
```

`ProviderHints` carry knobs like `image_encoding="inline"`, `use_gcs_upload=true`, etc.

### 7.2 Factory Changes
- `TextGeneratorFactory` detects presence of `ImageAttachment`s and chooses a provider implementation that advertises `SupportsVision`.
- Introduce `IVisionCapableModelProvider` interface exposing `Task<OutputResult> GenerateAsync(VisionRequest request, CancellationToken token)`.
- Backwards compatibility: existing text-only flow remains untouched; if model does not support vision and images are present, throw with suggestion.

## 8. Provider Responsibilities
### 8.1 OpenAI-compatible
- For `/v1/chat/completions` send messages array with `{"type":"input_text"}` + `{ "type":"input_image", "image_url": { "url": "data:image/png;base64,..." } }` segments ordered as provided.
- Honor `--image-format url` by uploading to a user-provided HTTPS endpoint (out-of-scope). For now only `inline` supported; command should reject `url` for OpenAI unless future work adds presigned upload logic.

### 8.2 Gemini API
- Call `models/{model}:generateContent` with `inlineData` blocks for images.
- Enforce Gemini’s max 20MB/file constraint pre-request.

### 8.3 llama.cpp / local models
- Determine whether currently loaded model exposes `vision` capability; require the server flag `--vision` or error.
- Use existing `/v1/chat/completions` compatibility layer; encode images base64 in `image_url` entries if server accepts.

## 9. Output & Logging
- `OutputResult` gains `IReadOnlyList<ImageAttachmentMetadata>` to echo attachments (without base64 payload) in textual/JSON outputs.
- Conversation logs store message array including references like `image://sha256/<hash>` to avoid bloating log files.
- Standard file output remains either plain text (assistant content only) or JSON (includes attachments metadata, provider response headers, usage stats).

## 10. Error Handling
- Aggregate validation errors to present a single report.
- On provider rejection (payload too large, unsupported format) surface original HTTP status/body via `LoggingHandler` at debug level.
- If only some images fail mid-request (rare), abort entire run to avoid partial/ambiguous outputs.

## 11. Configuration Surface
- Extend config files (text + JSON) with keys:
  - `vision.images`: array of paths or globs.
  - `vision.image_dir` + `vision.recursive`.
  - `vision.encoding`: `inline` | `url`.
  - `vision.max_pixels`.
  - `vision.provider_hints`: string map for advanced overrides.
- CLI args override config values. Env vars can mirror (e.g., `TEXTGEN_VISION_IMAGES`).

## 12. Telemetry & Metrics
- Update `LogManager` to include counters such as `vision.image_count`, total bytes, provider latencies.
- Emit warnings when near provider limits (e.g., > 18MB for Gemini) so users can react pre-failure.

## 13. Testing Strategy
- Unit tests for path collection/validation (mock filesystem).
- Provider-specific request builders with golden JSON snapshots.
- End-to-end smoke test using `samples/vision/` assets once models available; guard with feature flag to skip in CI if images missing.

## 14. Rollout Plan
1. Ship CLI flag parsing + validation behind `TEXTGEN_EXPERIMENTAL_VISION=1` environment gate.
2. Implement provider support incrementally (OpenAI first, then Gemini, then llama.cpp).
3. Document feature in README once first provider stable; keep docs/drafts here for internal iteration until then.

## 15. Open Questions
- Do we need resumable uploads for very large files? (blocked by provider support)
- Should we support video frames for future Gemini features?
- How do we redact sensitive pixels before logging? (possible hashing strategy?)

