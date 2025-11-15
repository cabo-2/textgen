using System.Collections.Generic;

#nullable enable

namespace Textgen
{
    public enum PromptInputType
    {
        Plain,
        Structured
    }

    public sealed class PromptMessage
    {
        public PromptMessage(string text, IEnumerable<ImageAttachment>? attachments = null)
        {
            Text = text ?? string.Empty;
            Attachments = attachments != null ? new List<ImageAttachment>(attachments) : new List<ImageAttachment>();
        }

        public string Text { get; }
        public IReadOnlyList<ImageAttachment> Attachments { get; }
    }

    public sealed class PromptSequence
    {
        private readonly List<PromptMessage> _messages;

        internal PromptSequence(List<PromptMessage> messages, PromptInputType type)
        {
            _messages = messages;
            Type = type;
        }

        public PromptInputType Type { get; }
        public IReadOnlyList<PromptMessage> Messages => _messages;
        public bool IsValid => _messages.Count > 0;
    }
}
