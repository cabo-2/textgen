namespace Textgen
{
    public interface IModelProvider
    {
        Task<List<string>> GetModelsAsync();
    }
}