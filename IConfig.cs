namespace Textgen
{
    public interface IConfig
    {
        Task<IConfig> LoadConfigAsync(string configFile, CancellationToken cancellationToken);
        string FormatConfig();
        IConfig LoadFromText(string textContent);
    }
}