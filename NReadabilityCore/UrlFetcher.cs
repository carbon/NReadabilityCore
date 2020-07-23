/*
 * NReadability
 * http://code.google.com/p/nreadability/
 * 
 * Copyright 2010 Marek Stój
 * http://immortal.pl/
 * 
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 * 
 * http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace NReadability
{
    public sealed class UrlFetcher : IUrlFetcher
    {
        private readonly HttpClient http = new HttpClient(new SocketsHttpHandler()
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Brotli | DecompressionMethods.Deflate,
            UseCookies = true,
            ConnectTimeout = TimeSpan.FromSeconds(5)
        })
        {
            DefaultRequestHeaders =
            {
                { "User-Agent", "Mozilla/5.0 (Windows NT 6.1; WOW64) AppleWebKit/537.31 (KHTML, like Gecko) Chrome/26.0.1410.64 Safari/537.31" },
                { "Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8" },
                { "Accept-Encoding", "gzip,deflate" }
            }
        };
       
      
        /*
        public string UploadValues(string url, NameValueCollection keyValuePairs)
        {
            return MakeRequest(url, () => _webClient.UploadValues(url, keyValuePairs));
        }
        */

        public async Task<string> FetchAsync(string url)
        {
            return await http.GetStringAsync(url).ConfigureAwait(false);
        }

    }
}
