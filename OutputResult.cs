using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

#nullable enable

namespace Textgen
{
    public class OutputResult
    {
        public string? Date { get; set; }
        public string? Host { get; set; }
        public string? Model { get; set; }
        public IConfig? Config { get; set; }
        public string? System { get; set; }
        public string? Prompt { get; set; }
        public string? Completion { get; set; }
        public List<Attachment> PromptAttachments { get; set; } = new();
        public List<Attachment> CompletionAttachments { get; set; } = new();
        public List<ConversationTurn> History { get; set; } = new();

        public string Format(string format)
        {
            switch (format?.ToLower())
            {
                case "json":
                    return JsonConvert.SerializeObject(this, Formatting.Indented);

                default:
                    var sb = new StringBuilder();
                    sb.Append($"@date\n{Date ?? string.Empty}\n\n");
                    sb.Append($"@host\n{Host ?? string.Empty}\n\n");
                    sb.Append($"@model\n{Model ?? string.Empty}\n\n");
                    sb.Append("@config\n");
                    if (Config != null)
                    {
                        sb.Append(Config.FormatConfig());
                    }
                    sb.Append("\n");
                    sb.Append($"@system\n{System ?? string.Empty}\n\n");

                    if (History.Count > 0)
                    {
                        for (int i = 0; i < History.Count; i++)
                        {
                            var history = History[i];
                            sb.Append($"@history-prompt_{i + 1}\n{history.Prompt ?? string.Empty}\n\n");
                            AppendAttachmentBlock(sb, $"history-prompt-attachments_{i + 1}", history.PromptAttachments);
                            sb.Append($"@history-completion_{i + 1}\n{history.Completion ?? string.Empty}\n\n");
                            AppendAttachmentBlock(sb, $"history-completion-attachments_{i + 1}", history.CompletionAttachments);
                        }
                    }

                    sb.Append($"@prompt\n{Prompt ?? string.Empty}\n\n");
                    AppendAttachmentBlock(sb, "prompt-attachments", PromptAttachments);
                    sb.Append($"@completion\n{Completion ?? string.Empty}\n\n");
                    AppendAttachmentBlock(sb, "completion-attachments", CompletionAttachments);

                    return sb.ToString().TrimEnd() + "\n";
            }
        }

        public static async Task<OutputResult> LoadFromFileAsync(string filePath, IConfig defaultConfig, CancellationToken cancellationToken)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("File not found", filePath);
            }

            var fileContent = await File.ReadAllTextAsync(filePath, cancellationToken);
            var result = filePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ? LoadFromJson(fileContent) : LoadFromText(fileContent, defaultConfig);
            result.Normalize();
            return result;
        }

        private static OutputResult LoadFromJson(string jsonContent)
        {
            var output = JsonConvert.DeserializeObject<OutputResult>(jsonContent) ?? new OutputResult();
            output.Normalize();
            return output;
        }

        private static OutputResult LoadFromText(string textContent, IConfig defaultConfig)
        {
            var lines = textContent.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            var result = new OutputResult();

            var historyPrompts = new Dictionary<int, string>();
            var historyCompletions = new Dictionary<int, string>();
            var historyPromptAttachments = new Dictionary<int, List<Attachment>>();
            var historyCompletionAttachments = new Dictionary<int, List<Attachment>>();

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];

                if (line.StartsWith("@date"))
                {
                    if (i + 1 < lines.Length)
                        result.Date = lines[++i];
                }
                else if (line.StartsWith("@host"))
                {
                    if (i + 1 < lines.Length)
                        result.Host = lines[++i];
                }
                else if (line.StartsWith("@config"))
                {
                    var configText = ReadBlock(lines, ref i, trim: false);
                    result.Config = defaultConfig.LoadFromText(configText);
                }
                else if (line.StartsWith("@model"))
                {
                    if (i + 1 < lines.Length)
                        result.Model = lines[++i];
                }
                else if (line.StartsWith("@system"))
                {
                    result.System = ReadBlock(lines, ref i, trim: true);
                }
                else if (line.StartsWith("@history-prompt-attachments_"))
                {
                    var index = ParseTrailingIndex(line, "@history-prompt-attachments_");
                    var blockLines = ReadBlockLines(lines, ref i);
                    historyPromptAttachments[index] = ParseAttachmentBlock(blockLines);
                }
                else if (line.StartsWith("@history-completion-attachments_"))
                {
                    var index = ParseTrailingIndex(line, "@history-completion-attachments_");
                    var blockLines = ReadBlockLines(lines, ref i);
                    historyCompletionAttachments[index] = ParseAttachmentBlock(blockLines);
                }
                else if (line.StartsWith("@history-prompt"))
                {
                    var index = int.Parse(line.Split('_')[1]);
                    historyPrompts[index] = ReadBlock(lines, ref i, trim: true);
                }
                else if (line.StartsWith("@history-completion"))
                {
                    var index = int.Parse(line.Split('_')[1]);
                    historyCompletions[index] = ReadBlock(lines, ref i, trim: true);
                }
                else if (line.StartsWith("@prompt-attachments"))
                {
                    var blockLines = ReadBlockLines(lines, ref i);
                    result.PromptAttachments = ParseAttachmentBlock(blockLines);
                }
                else if (line.StartsWith("@completion-attachments"))
                {
                    var blockLines = ReadBlockLines(lines, ref i);
                    result.CompletionAttachments = ParseAttachmentBlock(blockLines);
                }
                else if (line.StartsWith("@prompt"))
                {
                    result.Prompt = ReadBlock(lines, ref i, trim: true);
                }
                else if (line.StartsWith("@completion"))
                {
                    result.Completion = ReadBlock(lines, ref i, trim: true);
                }
            }

            var maxIndex = new[]
            {
                historyPrompts.Keys.DefaultIfEmpty(0).Max(),
                historyCompletions.Keys.DefaultIfEmpty(0).Max(),
                historyPromptAttachments.Keys.DefaultIfEmpty(0).Max(),
                historyCompletionAttachments.Keys.DefaultIfEmpty(0).Max()
            }.Max();

            for (int j = 1; j <= maxIndex; j++)
            {
                historyPrompts.TryGetValue(j, out var prompt);
                historyCompletions.TryGetValue(j, out var completion);
                historyPromptAttachments.TryGetValue(j, out var promptImages);
                historyCompletionAttachments.TryGetValue(j, out var completionImages);

                var entry = new ConversationTurn
                {
                    Prompt = prompt,
                    Completion = completion,
                    PromptAttachments = promptImages ?? new List<Attachment>(),
                    CompletionAttachments = completionImages ?? new List<Attachment>()
                };

                if (entry.HasContent)
                {
                    result.History.Add(entry);
                }
            }

            result.Normalize();
            return result;
        }

        private static string ReadBlock(string[] lines, ref int index, bool trim)
        {
            var builder = new StringBuilder();
            while (index + 1 < lines.Length && !lines[index + 1].StartsWith("@"))
            {
                builder.AppendLine(lines[++index]);
            }

            return trim ? builder.ToString().Trim() : builder.ToString();
        }

        private static List<string> ReadBlockLines(string[] lines, ref int index)
        {
            var list = new List<string>();
            while (index + 1 < lines.Length && !lines[index + 1].StartsWith("@"))
            {
                list.Add(lines[++index]);
            }
            return list;
        }

        private static List<Attachment> ParseAttachmentBlock(List<string> blockLines)
        {
            var attachments = new List<Attachment>();
            var pendingMeta = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var rawLine in blockLines)
            {
                if (string.IsNullOrWhiteSpace(rawLine))
                {
                    continue;
                }

                var trimmed = rawLine.Trim();
                if (trimmed.StartsWith("#"))
                {
                    var comment = trimmed.Substring(1).Trim();
                    var idx = comment.IndexOf('=');
                    if (idx > 0)
                    {
                        var key = comment.Substring(0, idx).Trim();
                        var value = comment.Substring(idx + 1).Trim();
                        pendingMeta[key] = value;
                    }
                    continue;
                }

                var path = ParsePathLineWithInlineComment(trimmed);
                if (string.IsNullOrWhiteSpace(path))
                {
                    pendingMeta.Clear();
                    continue;
                }

                var attachment = CreateAttachmentFromMetadata(path, pendingMeta);
                attachments.Add(attachment);
                pendingMeta.Clear();
            }

            return attachments;
        }

        private static Attachment CreateAttachmentFromMetadata(string path, IDictionary<string, string> meta)
        {
            meta.TryGetValue("type", out var type);
            var attachmentType = string.IsNullOrWhiteSpace(type) ? "image" : type.Trim();

            Attachment attachment = attachmentType.Equals("image", StringComparison.OrdinalIgnoreCase)
                ? new ImageAttachment()
                : new FileAttachment(attachmentType);

            attachment.StoredPath = path;
            attachment.FileName = Path.GetFileName(path);

            if (meta.TryGetValue("source_path", out var source))
            {
                attachment.SourcePath = source;
            }

            if (meta.TryGetValue("sha256", out var sha))
            {
                attachment.Sha256 = sha;
            }

            if (meta.TryGetValue("size", out var sizeString) || meta.TryGetValue("size_bytes", out sizeString))
            {
                if (long.TryParse(sizeString, out var size))
                {
                    attachment.SizeBytes = size;
                }
            }

            if (meta.TryGetValue("mime", out var mime))
            {
                attachment.MimeType = mime;
            }

            if (meta.TryGetValue("caption", out var caption))
            {
                attachment.Caption = caption;
            }

            return attachment;
        }

        private static string ParsePathLineWithInlineComment(string line)
        {
            var sb = new StringBuilder();
            bool inQuote = false;
            char quoteChar = '\0';
            for (int i = 0; i < line.Length; i++)
            {
                var ch = line[i];
                if (!inQuote && ch == '#')
                {
                    break;
                }

                if (ch == '\\' && inQuote && i + 1 < line.Length)
                {
                    var next = line[i + 1];
                    if (next == quoteChar || next == '\\')
                    {
                        sb.Append(next);
                        i++;
                        continue;
                    }
                }

                if (ch == '\'' || ch == '"')
                {
                    if (!inQuote)
                    {
                        inQuote = true;
                        quoteChar = ch;
                        continue;
                    }
                    else if (quoteChar == ch)
                    {
                        inQuote = false;
                        quoteChar = '\0';
                        continue;
                    }
                }

                sb.Append(ch);
            }

            var extracted = sb.ToString().Trim();
            if (extracted.Length == 0)
            {
                return string.Empty;
            }

            return extracted;
        }

        private static int ParseTrailingIndex(string line, string prefix)
        {
            var value = line.Substring(prefix.Length);
            if (!int.TryParse(value, out var index))
            {
                throw new FormatException($"Invalid attachment block header: {line}");
            }

            return index;
        }

        private static void AppendAttachmentBlock(StringBuilder sb, string blockName, IReadOnlyList<Attachment> attachments)
        {
            if (attachments == null || attachments.Count == 0)
            {
                return;
            }

            sb.Append($"@{blockName}\n");
            foreach (var attachment in attachments)
            {
                if (!string.IsNullOrWhiteSpace(attachment.Type))
                {
                    sb.Append($"# type={attachment.Type}\n");
                }

                if (!string.IsNullOrWhiteSpace(attachment.Sha256))
                {
                    sb.Append($"# sha256={attachment.Sha256}\n");
                }

                if (attachment.SizeBytes.HasValue)
                {
                    sb.Append($"# size={attachment.SizeBytes.Value}\n");
                }

                var path = attachment.StoredPath ?? attachment.SourcePath ?? attachment.FileName ?? string.Empty;
                sb.Append($"{FormatPath(path)}\n\n");
            }
        }

        private static string FormatPath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return path;
            }

            bool needsQuotes = path.IndexOfAny(new[] { ' ', '\t', '#', '"' , '\'' }) >= 0;
            if (!needsQuotes)
            {
                return path;
            }

            var escaped = path.Replace("\\", "\\\\").Replace("\"", "\\\"");
            return $"\"{escaped}\"";
        }

        private void Normalize()
        {
            PromptAttachments ??= new List<Attachment>();
            CompletionAttachments ??= new List<Attachment>();
            History ??= new List<ConversationTurn>();

            foreach (var turn in History)
            {
                if (turn == null)
                {
                    continue;
                }

                turn.PromptAttachments ??= new List<Attachment>();
                turn.CompletionAttachments ??= new List<Attachment>();
            }
        }
    }
}
