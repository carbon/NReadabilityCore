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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace Carbon.Readability
{
    public static class DomExtensions
    {
        public static XElement? GetBody(this XDocument document)
        {
            return document?.Root?.GetElementsByTagName("body").FirstOrDefault();
        }

        public static string? GetTitle(this XDocument document)
        {
            var documentRoot = document?.Root;

            if (documentRoot is null)
            {
                return null;
            }

            var headElement = documentRoot.GetElementsByTagName("head").FirstOrDefault();

            if (headElement is null)
            {
                return string.Empty;
            }

            var titleElement = headElement.GetChildrenByTagName("title").FirstOrDefault();

            if (titleElement is null)
            {
                return string.Empty;
            }

            return (titleElement.Value ?? "").Trim();
        }

        public static XElement GetElementById(this XDocument document, string id)
        {
            if (document == null)
            {
                throw new ArgumentNullException("document");
            }

            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException("id");
            }

            return
              (from element in document.Descendants()
               let idAttribute = element.Attribute("id")
               where idAttribute != null && idAttribute.Value == id
               select element).SingleOrDefault();
        }

        public static string GetId(this XElement element)
        {
            return element.GetAttributeValue("id", "");
        }

        public static void SetId(this XElement element, string id)
        {
            element.SetAttributeValue("id", id);
        }

        public static string GetClass(this XElement element)
        {
            return element.GetAttributeValue("class", "");
        }

        public static void SetClass(this XElement element, string @class)
        {
            element.SetAttributeValue("class", @class);
        }

        public static string GetStyle(this XElement element)
        {
            return element.GetAttributeValue("style", "");
        }

        public static void SetStyle(this XElement element, string? style)
        {
            element.SetAttributeValue("style", style);
        }

        public static string GetAttributeValue(this XElement element, string attributeName, string defaultValue)
        {
            XAttribute attribute = element.Attribute(attributeName);

            return attribute != null
                ? (attribute.Value ?? defaultValue)
                : defaultValue;
        }

        public static void SetAttributeValue(this XElement element, string attributeName, string value)
        {
            if (value == null)
            {
                var attribute = element.Attribute(attributeName);

                if (attribute != null)
                {
                    attribute.Remove();
                }
            }
            else
            {
                element.SetAttributeValue(attributeName, value);
            }
        }

        public static string GetAttributesString(this XElement element, string separator)
        {
            if (separator is null)
            {
                throw new ArgumentNullException(nameof(separator));
            }

            var resultSb = new StringBuilder();
            bool isFirst = true;

            element.Attributes().Aggregate(
              resultSb,
              (sb, attribute) =>
              {
                  string attributeValue = attribute.Value;

                  if (string.IsNullOrEmpty(attributeValue))
                  {
                      return sb;
                  }

                  if (!isFirst)
                  {
                      resultSb.Append(separator);
                  }

                  isFirst = false;

                  sb.Append(attribute.Value);

                  return sb;
              });

            return resultSb.ToString();
        }

        public static string GetInnerHtml(this XContainer container)
        {
            var sb = new StringBuilder();

            foreach (var childNode in container.Nodes())
            {
                sb.Append(childNode.ToString(SaveOptions.DisableFormatting));
            }

            return sb.ToString();
        }

        public static void SetInnerHtml(this XElement element, string html)
        {
            element.RemoveAll();

            var tmpElement = SgmlDomBuilder.BuildDocument(html);

            if (tmpElement.Root is null)
            {
                return;
            }

            foreach (var node in tmpElement.Root.Nodes())
            {
                element.Add(node);
            }
        }

        public static IEnumerable<XElement> GetElementsByTagName(this XContainer container, string tagName)
        {
            return container.Descendants().Where(e => tagName.Equals(e.Name.LocalName, StringComparison.OrdinalIgnoreCase));
        }

        public static int CountElementsByTagName(this XContainer container, string tagName)
        {
            return container.Descendants().Count(e => tagName.Equals(e.Name.LocalName, StringComparison.OrdinalIgnoreCase));
        }

        public static IEnumerable<XElement> GetChildrenByTagName(this XContainer container, string tagName)
        {          
            foreach (var childEl in container.Elements())
            {
                if (childEl.Name?.LocalName is string localName && localName.Equals(tagName, StringComparison.OrdinalIgnoreCase))
                {
                    yield return childEl;
                }
            }
        }

        public static XElement? FindFirstChildWithTagName(this XContainer container, string tagName)
        {
            foreach (var childEl in container.Elements())
            {
                if (childEl.Name?.LocalName is string localName && localName.Equals(tagName, StringComparison.OrdinalIgnoreCase))
                {
                    return childEl;
                }
            }

            return null;
        }
    }
}
