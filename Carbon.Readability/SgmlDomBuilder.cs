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
using System.Buffers;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Linq;

using Sgml;

namespace Carbon.Readability
{
    /// <summary>
    /// A class for constructing a DOM from HTML markup.
    /// </summary>
    public static class SgmlDomBuilder
    {
        /// <summary>
        /// Constructs a DOM (System.Xml.Linq.XDocument) from HTML markup.
        /// </summary>
        /// <param name="htmlContent">HTML markup from which the DOM is to be constructed.</param>
        /// <returns>System.Linq.Xml.XDocument instance which is a DOM of the provided HTML markup.</returns>
        public static XDocument BuildDocument(ReadOnlySpan<char> htmlContent)
        {
            if (htmlContent.Trim().Length == 0)
            {
                return new XDocument();
            }

            // "trim end" htmlContent to ...</html>$ (codinghorror.com puts some scripts after the </html> - sic!)

            const string htmlEnd = "</html";
            int indexOfHtmlEnd = htmlContent.LastIndexOf(htmlEnd);

            if (indexOfHtmlEnd != -1)
            {
                int relativeIndexOfHtmlEndBracket = htmlContent.Slice(indexOfHtmlEnd).IndexOf('>');

                if (relativeIndexOfHtmlEndBracket != -1)
                {
                    htmlContent = htmlContent.Slice(0, indexOfHtmlEnd + relativeIndexOfHtmlEndBracket + 1);
                }
            }

            XDocument document;

            try
            {
                document = LoadDocument(htmlContent);
            }
            catch (InvalidOperationException exc)
            {
                // sometimes SgmlReader doesn't handle <script> tags well and XDocument.Load() throws,
                // so we can retry with the html content with <script> tags stripped off

                if (!exc.Message.Contains("EndOfFile"))
                {
                    throw;
                }

                htmlContent = HtmlUtils.RemoveScriptTags(htmlContent);

                document = LoadDocument(htmlContent);
            }

            return document;
        }

        private static XDocument LoadDocument(ReadOnlySpan<char> htmlContent)
        {
            using var reader = new SgmlReader {
                CaseFolding = CaseFolding.ToLower,
                DocType = "HTML",
                WhitespaceHandling = WhitespaceHandling.None
            };

            var buffer = ArrayPool<byte>.Shared.Rent(htmlContent.Length * 2);

            try
            {
                int byteCount = Encoding.UTF8.GetBytes(htmlContent, buffer);

                using var sr = new StreamReader(new MemoryStream(buffer, 0, byteCount));

                reader.InputStream = sr;

                var document = XDocument.Load(reader);

                return document;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }
}
