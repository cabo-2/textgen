namespace Textgen
{
    public static class LogManager
    {
        public static async Task<OutputResult> LoadConversationAsync(
            string convLogFile,
            string convLogDir,
            IConfig defaultConfig,
            CancellationToken cancellationToken)
        {
            if (!string.IsNullOrEmpty(convLogFile) && File.Exists(convLogFile))
            {
                return await OutputResult.LoadFromFileAsync(convLogFile, defaultConfig, cancellationToken);
            }
            if (!string.IsNullOrEmpty(convLogDir) && Directory.Exists(convLogDir))
            {
                var files = Directory.GetFiles(convLogDir, "textgen_log_*.*")
                    .OrderByDescending(f => f)
                    .ToList();
                if (files.Count > 0)
                    return await OutputResult.LoadFromFileAsync(files[0], defaultConfig, cancellationToken);
            }
            return new OutputResult();
        }

        public static async Task<string> SaveConversationAsync(
            OutputResult conversation,
            string convLogFile,
            string convLogDir,
            string format,
            CancellationToken cancellationToken)
        {
            if (!string.IsNullOrEmpty(convLogFile))
            {
                var dir = Path.GetDirectoryName(convLogFile);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);
                await File.WriteAllTextAsync(convLogFile, conversation.Format(format), cancellationToken);
                return convLogFile;
            }
            else if (!string.IsNullOrEmpty(convLogDir))
            {
                Directory.CreateDirectory(convLogDir);
                string ts = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                string ext = format.Equals("json", StringComparison.OrdinalIgnoreCase) ? "json" : "txt";
                string fileName = $"textgen_log_{ts}.{ext}";
                string path = Path.Combine(convLogDir, fileName);
                await File.WriteAllTextAsync(path, conversation.Format(format), cancellationToken);
                return path;
            }
            return null;
        }

        public static async Task SaveOutputAsync(
            string formattedOutput,
            string outputFile,
            string outputDir,
            string format,
            CancellationToken cancellationToken)
        {
            if (!string.IsNullOrEmpty(outputFile))
            {
                var dir = Path.GetDirectoryName(outputFile);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);
                await File.WriteAllTextAsync(outputFile, formattedOutput, cancellationToken);
            }
            else if (!string.IsNullOrEmpty(outputDir))
            {
                Directory.CreateDirectory(outputDir);
                string ts = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                string ext = format.Equals("json", StringComparison.OrdinalIgnoreCase) ? "json" : "txt";
                string fileName = $"textgen_log_{ts}.{ext}";
                var path = Path.Combine(outputDir, fileName);
                await File.WriteAllTextAsync(path, formattedOutput, cancellationToken);
            }
        }
    }
}