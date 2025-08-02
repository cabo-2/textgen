namespace textgen
{
    /// <summary>Very small logging abstraction so we stay dependency-free.</summary>
    public interface ILogger
    {
        void Log(string message);
    }
}
