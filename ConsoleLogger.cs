namespace Textgen
{
    public sealed class ConsoleLogger : ILogger
    {
        public void Log(string message) =>
            Console.Error.WriteLine($"[DEBUG] {DateTime.UtcNow:O}  {message}");
    }
}
