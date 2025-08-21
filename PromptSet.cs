using System;
using System.Collections.Generic;
using System.Text;

namespace Textgen
{
    public enum PromptType
    {
        Plain,
        Structured
    }

    public class PromptSet
    {
        private PromptSet(List<string> prompts, PromptType type)
        {
            Prompts = prompts ?? new List<string>();
            Type = type;
        }

        public List<string> Prompts { get; private set; }
        public PromptType Type { get; private set; }

        public bool IsValid => Prompts.Count > 0;

        public static PromptSet Create(string prompt)
        {
            if (string.IsNullOrWhiteSpace(prompt))
            {
                return new PromptSet(new List<string>(), PromptType.Plain);
            }

            var prompts = new List<string>();

            if (TryParseStructuredPrompt(prompt, out var extractedPrompts))
            {
                return new PromptSet(extractedPrompts, PromptType.Structured);
            }
            else
            {
                // Treat as plain prompt
                prompts.Add(prompt.Trim());
                return new PromptSet(prompts, PromptType.Plain);
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

            // Return true if any structured prompts were found; otherwise, return false
            return foundStructured;
        }
    }
}