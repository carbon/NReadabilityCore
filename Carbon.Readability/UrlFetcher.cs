using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace Carbon.Readability
{
    public sealed class UrlFetcher : IUrlFetcher
    {
        private readonly HttpClient http = new HttpClient(new SocketsHttpHandler {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Brotli | DecompressionMethods.Deflate,
            UseCookies = true,
            ConnectTimeout = TimeSpan.FromSeconds(5)
        })
        {
            DefaultRequestHeaders = {
                { "User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/84.0.4147.89 Safari/537.36" },
                { "Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.9" },
                { "Accept-Encoding", "gzip, deflate, br" }
            }
        };

        public async Task<string> FetchAsync(string url)
        {
            return await http.GetStringAsync(url).ConfigureAwait(false);
        }
    }
}