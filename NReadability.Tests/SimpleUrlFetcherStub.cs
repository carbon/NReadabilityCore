using System;
using System.Threading.Tasks;

namespace NReadability.Tests
{
    public sealed class SimpleUrlFetcherStub : IUrlFetcher
    {
        private readonly string _contentToReturn;

        public SimpleUrlFetcherStub(string contentToReturn)
        {
            if (string.IsNullOrEmpty(contentToReturn))
            {
                throw new ArgumentException("Argument can't be null nor empty.", "contentToReturn");
            }

            _contentToReturn = contentToReturn;
        }

        public Task<string> FetchAsync(string url) => Task.FromResult(_contentToReturn);
    }
}
