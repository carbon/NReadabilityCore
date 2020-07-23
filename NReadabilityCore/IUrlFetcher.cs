using System.Threading.Tasks;

namespace NReadability
{
    public interface IUrlFetcher
    {
        Task<string> FetchAsync(string url);
    }
}
