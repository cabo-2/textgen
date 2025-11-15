using System.Collections.Generic;
using System.Linq;

#nullable enable

namespace Textgen
{
    public class ConversationTurn
    {
        public string? Prompt { get; set; }
        public List<ImageAttachment> PromptImages { get; set; } = new();
        public string? Completion { get; set; }
        public List<ImageAttachment> CompletionImages { get; set; } = new();

        public bool HasContent =>
            !string.IsNullOrWhiteSpace(Prompt) ||
            !string.IsNullOrWhiteSpace(Completion) ||
            (PromptImages?.Count > 0) ||
            (CompletionImages?.Count > 0);

        public ConversationTurn Clone()
        {
            return new ConversationTurn
            {
                Prompt = Prompt,
                Completion = Completion,
                PromptImages = PromptImages?.Select(p => p.Clone()).ToList() ?? new List<ImageAttachment>(),
                CompletionImages = CompletionImages?.Select(p => p.Clone()).ToList() ?? new List<ImageAttachment>()
            };
        }
    }
}
