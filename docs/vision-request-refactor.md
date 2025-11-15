# Generation Request Refactor Proposal

## Background
- The current CLI accepts a `string prompt` plus optional `promptFile`. `PromptSet` only extracts multiple `@prompt` entries from structured text but still produces a bare `List<string>`.
- `TextGenerator.GenerateTextAsync` operates on `(model, prompt, system, config, conversationLog)` without awareness of attachments. `OutputResult` represents past turns as text-only `(Prompt, Completion)` tuples.
- The upcoming vision feature needs to route prompt-side images, potential assistant images, and history attachments to providers. Treating prompts as strings while bolting on images later would create duplicated parsing logic and brittle conversions.

## Goals
1. Introduce a `GenerationRequest` object that encapsulates both text and attachment metadata for every message that leaves the CLI.
2. Replace `PromptSet` with a richer abstraction that preserves structured prompts (`@prompt`, `@prompt-images`, history blocks) and feeds them directly into `GenerationRequest`.
3. Keep backward compatibility for text-only flows while enabling providers to opt into `SupportsVision`.
4. Make it easy to perform pre-validation (file existence, hashing) once, before dispatching to any provider.

## Proposed Types

### `GenerationRequest`
```csharp
public sealed record GenerationRequest(
    string Model,
    string? SystemPrompt,
    PromptSequence Sequence,
    IConfig Config,
    ConversationState Conversation,
    bool VisionOnly);
```
- `PromptSequence` contains the prompts collected for the current run (see below).
- `ConversationState` represents the existing transcript loaded from `OutputResult` (text + attachments) so generators can send full history.
- The request is provider-agnostic; individual generators translate it to their API payloads.

### `PromptSequence`
```csharp
public sealed record PromptSequence(
    IReadOnlyList<PromptMessage> Messages,
    PromptInputType Type);
```
- Replaces `PromptSet`. `PromptInputType` captures former `Plain` vs `Structured`.
- `Messages` represent each turn the batch runner needs to execute. For plain prompts this is a single message; for multi `@prompt` files there may be several.

### `PromptMessage`
```csharp
public sealed record PromptMessage(
    string Text,
    IReadOnlyList<ImageAttachment> Attachments);
```
- `Attachments` already follow the metadata shape defined in the spec (original filename, canonical stored path, MIME, size, SHA256, caption, role = `prompt`).
- Batch mode iterates over `PromptSequence.Messages`, building a fresh `GenerationRequest` per entry (preserving attachments per prompt).

### `ConversationState`
```csharp
public sealed record ConversationState(
    IReadOnlyList<ConversationTurn> Turns,
    ConversationTurn? Current);

public sealed record ConversationTurn(
    string Role, // "user" or "assistant"
    string? Text,
    IReadOnlyList<ImageAttachment> Attachments);
```
- `OutputResult` serialization/deserialization feeds this structure, so generators no longer unpack tuples manually.

## Building a `GenerationRequest`
1. **Parse CLI/config input** using a `PromptSequenceBuilder`. It accepts:
   - Raw prompt string or path.
   - Parsed `OutputResult` (for history).
   - Attachment descriptors from CLI flags (`--image`, `--image-dir`, etc.).
2. The builder:
   - Detects structured prompt documents (`@prompt`, `@prompt-images`, `@history-*`) and materializes `PromptMessage` objects with text + attachments.
   - Copies attachment files into the run’s `images/` directory using their original filename (per spec) and computes metadata once.
   - Produces a `PromptSequence` plus a normalized `ConversationState`.
3. `GenerationRequestFactory.Create(...)` takes model/system/config plus builder output and returns an immutable `GenerationRequest`.

## Generator Integration
- Update `TextGenerator` to expose `Task<OutputResult> GenerateAsync(GenerationRequest request, CancellationToken token)`.
- Provide a default adapter so existing generators can keep a `GenerateTextAsync` overload until fully migrated:
  ```csharp
  public abstract Task<OutputResult> GenerateAsync(GenerationRequest request, CancellationToken token);

  public virtual Task<OutputResult> GenerateTextAsync(string model, string prompt, ...) =>
      GenerateAsync(GenerationRequest.Legacy(model, prompt, ...));
  ```
- Providers that support images override `GenerateAsync` to inspect `request.Sequence`, iterate over attachments, and produce multimodal payloads.

## Migration Plan
1. **Phase 0 – groundwork**
   - Implement `ImageAttachment` + hashing logic in shared utility, honoring original filenames under `images/`.
   - Extend `OutputResult` parsing/formatting to handle `@prompt-images`, `@completion-images`, and history equivalents so conversation logs can round-trip attachments.
2. **Phase 1 – Prompt refactor**
   - Introduce `PromptSequence`, `PromptSequenceBuilder`, and `ConversationState`.
   - Adjust CLI batch flow to consume `PromptSequence` instead of `PromptSet`.
   - Keep text-only path by creating `PromptMessage` with empty attachment lists.
3. **Phase 2 – Request plumbing**
   - Create `GenerationRequest` + `GenerationRequestFactory`.
   - Update `TextGenerator` base and concrete implementations to accept the new shape while still handling legacy calls (temporary shim).
4. **Phase 3 – Provider-specific updates**
   - For OpenAI/Gemini/llama.cpp, translate `GenerationRequest` into their respective payloads (respecting attachment roles and order).
   - Add validation for unsupported cases (vision request targeting a text-only model).

## Benefits
- Single source of truth for prompts and attachments across CLI, providers, and output logging.
- Deterministic file naming and metadata reuse reduce chances of mismatched attachments.
- Refactor lays groundwork for future extensions (assistant images, tool outputs) without reworking the CLI again.

## API compatibility
- **OpenAI `/v1/chat/completions`**: Each `GenerationRequest` message already matches the role/content structure OpenAI expects; multimodal runs simply add `input_image` segments so text-only payloads remain unchanged.
- **llama.cpp `/completion`**: The adapter continues to emit the legacy plain-text prompt when no attachments exist, and introduces the necessary image metadata only when targeting servers launched with `--vision`, keeping compatibility with today’s llama flows.
- **Google Gemini**: Requests convert to `generateContent` calls with `inlineData` image parts, mirroring what the standalone Gemini generator already sends, so no behavioral differences for text-only usage.
