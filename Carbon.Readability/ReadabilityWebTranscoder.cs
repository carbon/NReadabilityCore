﻿/*
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
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Carbon.Readability
{
    public partial class ReadabilityWebTranscoder
    {
        private const int maxPages = 30;
        private const string pageIdPrefix = "readability-page-";

        private static readonly Func<int, string> _DefaultPageSeparatorBuilder =
          pageNumber => $"<p class='page-separator' title='Page {pageNumber}'>&sect;</p>";

        private readonly ReadabilityTranscoder _transcoder;
        private readonly IUrlFetcher _urlFetcher;
        private readonly SgmlDomSerializer _sgmlDomSerializer;

        private Func<int, string> _pageSeparatorBuilder;
        private List<string>? _parsedPages;
        private int _curPageNum;

        #region Constructor(s)

        /// <summary>
        ///  Initializes a new instance of NReadabilityWebTranscoder.
        ///  Allows passing in custom-constructed ReadabilityTranscoder,
        ///  and a custom IUrlFetcher.
        /// </summary>
        /// <param name="transcoder">A ReadabilityTranscoder.</param>
        /// <param name="urlFetcher">IFetcher instance to download content.</param>
        /// <param name="pageSeparatorBuilder">A function that creates a HTML fragment for page separator. It takes the page number as an argument.</param>
        public ReadabilityWebTranscoder(ReadabilityTranscoder transcoder, IUrlFetcher urlFetcher, Func<int, string> pageSeparatorBuilder)
        {
            _transcoder = transcoder;
            _urlFetcher = urlFetcher;
            _sgmlDomSerializer = new SgmlDomSerializer();
            _pageSeparatorBuilder = pageSeparatorBuilder;
        }

        /// <summary>
        ///  Initializes a new instance of NReadabilityWebTranscoder.
        ///  Allows passing in custom-constructed ReadabilityTranscoder,
        ///  and a custom IUrlFetcher.
        /// </summary>
        /// <param name="transcoder">A ReadabilityTranscoder.</param>
        /// <param name="urlFetcher">IFetcher instance to download content.</param>
        public ReadabilityWebTranscoder(ReadabilityTranscoder transcoder, IUrlFetcher urlFetcher)
          : this(transcoder, urlFetcher, _DefaultPageSeparatorBuilder)
        {
        }

        /// <summary>
        /// Initializes a new instance of NReadabilityWebTranscoder.
        /// Allows passing in custom-constructed ReadabilityTranscoder.
        /// </summary>
        /// <param name="transcoder">A NReadailityTranscoder.</param>
        public ReadabilityWebTranscoder(ReadabilityTranscoder transcoder)
          : this(transcoder, new UrlFetcher())
        {
        }

        /// <summary>
        /// Initializes a new instance of NReadabilityWebTranscoder.
        /// </summary>
        public ReadabilityWebTranscoder()
          : this(new ReadabilityTranscoder())
        {
        }

        #endregion

        #region Public methods

        /// <summary>
        /// Extracts article content from an HTML page at the given URL.
        /// </summary>
        /// <param name="request">An object containing input parameters, i.a. URL of the page to be processed.</param>
        /// <returns>An object containing transcoding result, i.a. extracted content and title.</returns>
        public async Task<TranscodeResult> TranscodeAsync(WebTranscodeRequest request)
        {
            return await DoTranscodeAsync(request.Url, request.DomSerializationParams).ConfigureAwait(false);           
        }

        /// <summary>
        /// Extracts main article content from a HTML web page.
        /// </summary>    
        /// <param name="url">Url from which the content was downloaded. Used to resolve relative urls. Can be null.</param>
        /// <param name="domSerializationParams">Contains parameters that modify the behaviour of the output serialization.</param>
        /// <param name="mainContentExtracted">Determines whether the content has been extracted (if the article is not empty).</param>    
        /// <returns>HTML markup containing extracted article content.</returns>
        [Obsolete("Use TranscodingResult Transcode(TranscodingInput) method.")]
        public Task<TranscodeResult> TranscodeAsync(string url, DomSerializationParams domSerializationParams)
        {
            return DoTranscodeAsync(url, domSerializationParams);
        }

        /// <summary>
        /// Extracts main article content from a HTML web page using default DomSerializationParams.
        /// </summary>    
        /// <param name="url">Url from which the content was downloaded. Used to resolve relative urls. Can be null.</param>
        /// <param name="mainContentExtracted">Determines whether the content has been extracted (if the article is not empty).</param>    
        /// <returns>HTML markup containing extracted article content.</returns>
        [Obsolete("Use TranscodingResult Transcode(TranscodingInput) method.")]
        public Task<TranscodeResult> TranscodeAsync(string url)
        {
            return DoTranscodeAsync(url, DomSerializationParams.CreateDefault());
        }

        #endregion
        #region Private helper methods

        private async Task<TranscodeResult> DoTranscodeAsync(string url, DomSerializationParams domSerializationParams)
        {


            _curPageNum = 1;
            _parsedPages = new List<string>();

            /* Make sure this document is added to the list of parsed pages first, so we don't double up on the first page */
            _parsedPages.Add(Regex.Replace(url, @"\/$", ""));

            string htmlContent = await _urlFetcher.FetchAsync(url).ConfigureAwait(false);

            /* If we can't fetch the page, then exit. */
            if (string.IsNullOrEmpty(htmlContent))
            {
                return new TranscodeResult(false);
            }

            /* Attempt to transcode the page */
            XDocument document;

            document = _transcoder.TranscodeToXml(htmlContent, url,
                out bool mainContentExtracted,
                out string extractedTitle,
                out string? nextPage);

            if (nextPage != null)
            {
                await AppendNextPageAsync(document, nextPage).ConfigureAwait(false);
            }

            /* If there are multiple pages, rename the first content div */
            if (_curPageNum > 1)
            {
                var articleContainer = document.GetElementById("readInner").Element("div");

                articleContainer.SetId(pageIdPrefix + "1");
                articleContainer.SetClass("page");
            }

            string content = _sgmlDomSerializer.Serialize(document, domSerializationParams);

            return new TranscodeResult(mainContentExtracted)
            {
                Content = content,
                Title = extractedTitle
            };
        }

        /// <summary>
        /// Recursively appends subsequent pages of a multipage article.
        /// </summary>
        /// <param name="document">Compiled document</param>
        /// <param name="url">Url of current page</param>
        private async Task AppendNextPageAsync(XDocument document, string url)
        {
            _curPageNum++;

            var contentDiv = document.GetElementById("readInner");

            if (_curPageNum > maxPages)
            {
                url = "<div style='text-align: center'><a href='" + url + "'>View Next Page</a></div>";
                contentDiv.Add(XDocument.Parse(url));
                return;
            }

            string nextContent = await _urlFetcher.FetchAsync(url).ConfigureAwait(false);

            if (string.IsNullOrEmpty(nextContent))
            {
                return;
            }

            var nextDocument = _transcoder.TranscodeToXml(
                nextContent,
                url,
                out bool mainContentExtracted,
                out string extractedTitle,
                out string? nextPageLink);

            var nextInner = nextDocument.GetElementById("readInner");
            var header = nextInner.Element("h1");

            if (header != null)
            {
                header.Remove();
            }

            /*
             * Anti-duplicate mechanism. Essentially, get the first paragraph of our new page.
             * Compare it against all of the the previous document's we've gotten. If the previous
             * document contains exactly the innerHTML of this first paragraph, it's probably a duplicate.
            */
            var firstP = nextInner.GetElementsByTagName("p").Count() > 0 ? nextInner.GetElementsByTagName("p").First() : null;

            if (firstP != null && firstP.GetInnerHtml().Length > 100)
            {
                //string innerHtml = firstP.GetInnerHtml();
                //var existingContent = contentDiv.GetInnerHtml();        
                //existingContent = Regex.Replace(existingContent, "xmlns(:[a-z]+)?=['\"][^'\"]+['\"]", "", RegexOptions.IgnoreCase);
                //existingContent = Regex.Replace(existingContent, @"\s+", "");
                //innerHtml = Regex.Replace(innerHtml, @"\s+", "");

                // TODO: This test could probably be improved to compare the actual markup.
                string existingContent = contentDiv.Value;
                string innerHtml = firstP.Value;

                if (!string.IsNullOrEmpty(existingContent) && !string.IsNullOrEmpty(innerHtml) && existingContent.IndexOf(innerHtml, StringComparison.OrdinalIgnoreCase) != -1)
                {
                    _parsedPages.Add(url);
                    return;
                }
            }

            /* Add the content to the existing html */
            var nextDiv = new XElement("div");

            if (_pageSeparatorBuilder != null)
            {
                nextDiv.SetInnerHtml(_pageSeparatorBuilder(_curPageNum));
            }

            nextDiv.SetId(pageIdPrefix + _curPageNum);
            nextDiv.SetClass("page");
            nextDiv.Add(nextInner.Nodes());
            contentDiv.Add(nextDiv);
            _parsedPages.Add(url);

            /* Only continue if we haven't already seen the next page page */
            if (!string.IsNullOrEmpty(nextPageLink) && !_parsedPages.Contains(nextPageLink))
            {
                await AppendNextPageAsync(document, nextPageLink).ConfigureAwait(false);
            }
        }

        #endregion

        /// <summary>
        /// A function which, given a current page number, constructs the HTML which will be used as a page separator.
        /// </summary>
        public Func<int, string> PageSeparatorBuilder
        {
            get => _pageSeparatorBuilder;
            set => _pageSeparatorBuilder = value;
        }
    }
}
