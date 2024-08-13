using System;
using System.Collections.Generic;
using System.Text;

namespace textgen
{
    public class StructuredPrompt
    {
        public StructuredPrompt(string prompt)
        {
            Prompts = new List<string>();
            ParsePrompt(prompt);
        }

        public List<string> Prompts { get; private set; }

        public bool IsValidStructuredPrompt => Prompts.Count > 0;

        private void ParsePrompt(string prompt)
        {
            if (string.IsNullOrWhiteSpace(prompt))
            {
                return;
            }

            var lines = prompt.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();

                if (line.StartsWith("@prompt"))
                {
                    StringBuilder promptText = new StringBuilder();
                    while (i + 1 < lines.Length && !lines[i + 1].StartsWith("@"))
                    {
                        promptText.AppendLine(lines[++i]);
                    }
                    Prompts.Add(promptText.ToString().Trim());
                }
            }
        }
    }
}