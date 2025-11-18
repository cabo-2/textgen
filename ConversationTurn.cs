using System.Collections.Generic;
using System.Linq;

#nullable enable

namespace Textgen
{
    public class ConversationTurn
    {
        public string? Prompt { get; set; }
        public List<Attachment> PromptAttachments { get; set; } = new();
        public string? Completion { get; set; }
        public List<Attachment> CompletionAttachments { get; set; } = new();

        public bool HasContent =>
            !string.IsNullOrWhiteSpace(Prompt) ||
            !string.IsNullOrWhiteSpace(Completion) ||
            (PromptAttachments?.Count > 0) ||
            (CompletionAttachments?.Count > 0);

        public ConversationTurn Clone()
        {
            return new ConversationTurn
            {
                Prompt = Prompt,
                Completion = Completion,
                PromptAttachments = PromptAttachments?.Select(p => p.Clone()).ToList() ?? new List<Attachment>(),
                CompletionAttachments = CompletionAttachments?.Select(p => p.Clone()).ToList() ?? new List<Attachment>()
            };
        }
    }
}
