using System.Threading.Tasks;

namespace Carbon.Readability.Tests
{
    internal sealed class SimpleUrlFetcherStub : IUrlFetcher
    {
        private readonly string _contentToReturn;

        public SimpleUrlFetcherStub(string contentToReturn)
        {
            _contentToReturn = contentToReturn;
        }

        public Task<string> FetchAsync(string url) => Task.FromResult(_contentToReturn);
    }
}
