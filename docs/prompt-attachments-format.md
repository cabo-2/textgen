# Vision & Attachment Logging Spec

Single document capturing the latest attachment format and behavior.

## 1. Log format (OutputResult)
- Blocks are unified as `@*-attachments` (e.g., `@prompt-attachments`, `@completion-attachments`, `@history-prompt-attachments_1`). Do not emit `@*-images`.
- One path line per attachment, with metadata comments immediately before the path.
- `# comment` to the right of a path line is ignored (human-readable note only).

### Block example
```
@prompt-attachments
# type=image
# sha256=30841d1b537829ef6dbad4a939606f597ef01f33ebfd5a5eff706a19d4967e8e
# size=3435587
"images/img 01 diagram.png"   # relative to the working directory

# type=image
# sha256=f20ec5e1d7817476b32a92e8454d872bca355879b8a4b13ff3e8a47cef91661e
# size=85500
images/img_02_logs.png
```

### Parse / write rules
- The block ends at the next line starting with `@`. Blank lines are ignored.
- Metadata lines are `# key=value` and apply to the next path only (then reset).
- Expected keys: `type`, `sha256`, `size`. Extra keys like `caption`, `source_path`, `mime` are allowed.
- Paths:
  - Quoted: use the inner text as-is (supports spaces; escape with `\"` or `\'`).
  - Unquoted: leading/trailing whitespace not allowed; `#` starts a comment.
- Relative paths are interpreted from the process working directory at runtime. Absolute paths are readable but should be redacted before sharing logs.
- Writers auto-quote when needed and emit `# type/sha256/size` immediately before the path.

### Data structures
- `ConversationTurn`: `PromptAttachments` / `CompletionAttachments`
- `OutputResult`: `PromptAttachments` / `CompletionAttachments` / `History` (history also emits `@history-*-attachments_X`)
- Semantics: only list order is preserved; no counts or numbering.

## 2. Attachment type handling
- Model: abstract `Attachment` with `ImageAttachment` / `FileAttachment` (the latter keeps only a type string).
- OpenAI/Gemini: **send images only**. If non-images exist, log a warning and drop them.
- Llama: attachments unsupported; warn and clear the list before sending (not kept in history).

## 3. CLI / scripts
- Attachments: `-a|--attachment <FILE>` (repeatable).
- Error only when `--prompt`, `--system`, and attachments are all absent.
- Non-image files are accepted but later filtered by providers (OpenAI/Gemini warn+drop, Llama drops all).
- `scripts/vision_dir.sh` also uses `-a`.

## 4. Notes for future extensions
- `type` is free-form today (`image` for images). We may standardize (`audio`/`video`/`document`, etc.) later.
- To preserve captions or source paths, add them as comment keys; the parser keeps unknown keys.
