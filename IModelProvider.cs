using System.Collections.Generic;
using System.Threading.Tasks;

namespace textgen
{
    public interface IModelProvider
    {
        Task<List<string>> GetModelsAsync();
    }
}