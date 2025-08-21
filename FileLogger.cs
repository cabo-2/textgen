using System;
using System.IO;

namespace Textgen
{
    public sealed class FileLogger : ILogger, IDisposable
    {
        private readonly StreamWriter _writer;

        public FileLogger(string path)
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            _writer = new StreamWriter(path, append: true) { AutoFlush = true };
        }

        public void Log(string message) =>
            _writer.WriteLine($"[DEBUG] {DateTime.UtcNow:O}  {message}");

        public void Dispose() => _writer.Dispose();
    }
}
