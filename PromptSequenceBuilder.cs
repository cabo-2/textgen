using System.Collections.Generic;
using System;
using System.Linq;
using System.Text;

namespace Textgen
{
    public static class PromptSequenceBuilder
    {
        public static PromptSequence FromString(string prompt)
        {
            if (string.IsNullOrWhiteSpace(prompt))
            {
                return new PromptSequence(new List<PromptMessage>(), PromptInputType.Plain);
            }

            if (TryParseStructuredPrompt(prompt, out var prompts))
            {
                var structuredMessages = prompts
                    .Select(p => new PromptMessage(p))
                    .ToList();
                return new PromptSequence(structuredMessages, PromptInputType.Structured);
            }
            else
            {
                var messages = new List<PromptMessage>
                {
                    new PromptMessage(prompt.Trim())
                };
                return new PromptSequence(messages, PromptInputType.Plain);
            }
        }

        private static bool TryParseStructuredPrompt(string prompt, out List<string> prompts)
        {
            prompts = new List<string>();
            var lines = prompt.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            bool foundStructured = false;

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();

                if (line.StartsWith("@prompt"))
                {
                    foundStructured = true;
                    StringBuilder promptText = new StringBuilder();
                    while (i + 1 < lines.Length && !lines[i + 1].StartsWith("@"))
                    {
                        promptText.AppendLine(lines[++i]);
                    }
                    prompts.Add(promptText.ToString().Trim());
                }
            }

            return foundStructured;
        }
    }
}
