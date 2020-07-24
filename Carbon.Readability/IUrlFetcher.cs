using System.Threading.Tasks;

namespace Carbon.Readability
{
    public interface IUrlFetcher
    {
        Task<string> FetchAsync(string url);
    }
}
