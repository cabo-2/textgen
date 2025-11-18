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
        public List<Attachment> PromptImages { get; set; } = new();
        public List<Attachment> CompletionImages { get; set; } = new();
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
                            AppendImageBlock(sb, $"history-prompt-images_{i + 1}", $"history_prompt_{i + 1}_image_", history.PromptImages);
                            sb.Append($"@history-completion_{i + 1}\n{history.Completion ?? string.Empty}\n\n");
                            AppendImageBlock(sb, $"history-completion-images_{i + 1}", $"history_completion_{i + 1}_image_", history.CompletionImages);
                        }
                    }

                    sb.Append($"@prompt\n{Prompt ?? string.Empty}\n\n");
                    AppendImageBlock(sb, "prompt-images", "prompt_image_", PromptImages);
                    sb.Append($"@completion\n{Completion ?? string.Empty}\n\n");
                    AppendImageBlock(sb, "completion-images", "completion_image_", CompletionImages);

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
            var historyPromptImages = new Dictionary<int, List<Attachment>>();
            var historyCompletionImages = new Dictionary<int, List<Attachment>>();

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
                else if (line.StartsWith("@history-prompt-images_"))
                {
                    var index = ParseTrailingIndex(line, "@history-prompt-images_");
                    var blockLines = ReadBlockLines(lines, ref i);
                    historyPromptImages[index] = ParseAttachmentBlock(blockLines, $"history_prompt_{index}_image_");
                }
                else if (line.StartsWith("@history-completion-images_"))
                {
                    var index = ParseTrailingIndex(line, "@history-completion-images_");
                    var blockLines = ReadBlockLines(lines, ref i);
                    historyCompletionImages[index] = ParseAttachmentBlock(blockLines, $"history_completion_{index}_image_");
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
                else if (line.StartsWith("@prompt-images"))
                {
                    var blockLines = ReadBlockLines(lines, ref i);
                    result.PromptImages = ParseAttachmentBlock(blockLines, "prompt_image_");
                }
                else if (line.StartsWith("@completion-images"))
                {
                    var blockLines = ReadBlockLines(lines, ref i);
                    result.CompletionImages = ParseAttachmentBlock(blockLines, "completion_image_");
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
                historyPromptImages.Keys.DefaultIfEmpty(0).Max(),
                historyCompletionImages.Keys.DefaultIfEmpty(0).Max()
            }.Max();

            for (int j = 1; j <= maxIndex; j++)
            {
                historyPrompts.TryGetValue(j, out var prompt);
                historyCompletions.TryGetValue(j, out var completion);
                historyPromptImages.TryGetValue(j, out var promptImages);
                historyCompletionImages.TryGetValue(j, out var completionImages);

                var entry = new ConversationTurn
                {
                    Prompt = prompt,
                    Completion = completion,
                    PromptImages = promptImages ?? new List<Attachment>(),
                    CompletionImages = completionImages ?? new List<Attachment>()
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

        private static List<Attachment> ParseAttachmentBlock(List<string> blockLines, string keyPrefix)
        {
            var attachments = new SortedDictionary<int, Attachment>();
            foreach (var rawLine in blockLines)
            {
                if (string.IsNullOrWhiteSpace(rawLine))
                {
                    continue;
                }

                var trimmed = rawLine.Trim();
                if (trimmed.StartsWith("count=", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var separatorIndex = trimmed.IndexOf('=');
                if (separatorIndex < 0)
                {
                    continue;
                }

                var key = trimmed.Substring(0, separatorIndex);
                var value = trimmed.Substring(separatorIndex + 1);

                if (!key.StartsWith(keyPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var remainder = key.Substring(keyPrefix.Length);
                var underscoreIndex = remainder.IndexOf('_');
                if (underscoreIndex < 0)
                {
                    continue;
                }

                var indexPart = remainder.Substring(0, underscoreIndex);
                if (!int.TryParse(indexPart, out var attachmentIndex))
                {
                    continue;
                }

                var field = remainder.Substring(underscoreIndex + 1);
                if (!attachments.TryGetValue(attachmentIndex, out var attachment))
                {
                    attachment = new ImageAttachment();
                    attachments[attachmentIndex] = attachment;
                }

                ApplyAttachmentField(attachment, field, value);
            }

            return attachments
                .OrderBy(kvp => kvp.Key)
                .Select(kvp => kvp.Value)
                .ToList();
        }

        private static void ApplyAttachmentField(Attachment attachment, string field, string value)
        {
            switch (field)
            {
                case "path":
                    attachment.StoredPath = value;
                    if (string.IsNullOrEmpty(attachment.FileName))
                    {
                        attachment.FileName = Path.GetFileName(value);
                    }
                    break;
                case "original_path":
                    attachment.SourcePath = value;
                    break;
                case "sha256":
                    attachment.Sha256 = value;
                    break;
                case "mime":
                    attachment.MimeType = value;
                    break;
                case "size_bytes":
                    if (long.TryParse(value, out var size))
                    {
                        attachment.SizeBytes = size;
                    }
                    break;
                case "caption":
                    attachment.Caption = value;
                    break;
            }
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

        private static void AppendImageBlock(StringBuilder sb, string blockName, string keyPrefix, IReadOnlyList<Attachment> attachments)
        {
            if (attachments == null || attachments.Count == 0)
            {
                return;
            }

            sb.Append($"@{blockName}\n");
            sb.Append($"count={attachments.Count}\n");
            for (int i = 0; i < attachments.Count; i++)
            {
                var attachment = attachments[i];
                var prefix = $"{keyPrefix}{i + 1}_";
                sb.Append($"{prefix}path={attachment.StoredPath ?? attachment.SourcePath ?? attachment.FileName ?? string.Empty}\n");
                if (!string.IsNullOrEmpty(attachment.SourcePath))
                {
                    sb.Append($"{prefix}original_path={attachment.SourcePath}\n");
                }
                if (!string.IsNullOrEmpty(attachment.Sha256))
                {
                    sb.Append($"{prefix}sha256={attachment.Sha256}\n");
                }
                if (!string.IsNullOrEmpty(attachment.MimeType))
                {
                    sb.Append($"{prefix}mime={attachment.MimeType}\n");
                }
                if (attachment.SizeBytes.HasValue)
                {
                    sb.Append($"{prefix}size_bytes={attachment.SizeBytes.Value}\n");
                }
                if (!string.IsNullOrEmpty(attachment.Caption))
                {
                    sb.Append($"{prefix}caption={attachment.Caption}\n");
                }
            }
            sb.Append("\n");
        }

        private void Normalize()
        {
            PromptImages ??= new List<Attachment>();
            CompletionImages ??= new List<Attachment>();
            History ??= new List<ConversationTurn>();

            foreach (var turn in History)
            {
                if (turn == null)
                {
                    continue;
                }

                turn.PromptImages ??= new List<Attachment>();
                turn.CompletionImages ??= new List<Attachment>();
            }
        }
    }
}
