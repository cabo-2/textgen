using System.Collections.Generic;
using System.Threading.Tasks;

namespace Textgen
{
    public interface IModelProvider
    {
        Task<List<string>> GetModelsAsync();
    }
}