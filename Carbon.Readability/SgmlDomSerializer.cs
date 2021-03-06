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
using System.Linq;
using System.Xml.Linq;

namespace Carbon.Readability
{
    /// <summary>
    /// A class for serializing a DOM to string.
    /// </summary>
    public sealed class SgmlDomSerializer
    {
        /// <summary>
        /// Serializes given DOM (System.Xml.Linq.XDocument object) to a string.
        /// </summary>
        /// <param name="document">System.Xml.Linq.XDocument instance containing the DOM to be serialized.</param>
        /// <param name="domSerializationParams">Contains parameters that modify the behaviour of the output serialization.</param>
        /// <returns>Serialized representation of the DOM.</returns>
        public string Serialize(XDocument document, DomSerializationParams domSerializationParams)
        {
            if (!domSerializationParams.DontIncludeContentTypeMetaElement
             || !domSerializationParams.DontIncludeMobileSpecificMetaElements
             || !domSerializationParams.DontIncludeGeneratorMetaElement)
            {
                var documentRoot = document.Root;

                if (documentRoot == null)
                {
                    throw new ArgumentException("The document must have a root.");
                }

                if (documentRoot.Name == null || !"html".Equals(documentRoot.Name.LocalName, StringComparison.OrdinalIgnoreCase))
                {
                    throw new ArgumentException("The document's root must be an html element.");
                }

                // add <head> element if not present
                var headElement = documentRoot.GetChildrenByTagName("head").FirstOrDefault();

                if (headElement == null)
                {
                    headElement = new XElement("head");
                    documentRoot.AddFirst(headElement);
                }

                ProcessMetaElements(headElement, domSerializationParams);
            }


            string result = document.ToString(domSerializationParams.PrettyPrint ? SaveOptions.None : SaveOptions.DisableFormatting);


            return result;
        }

        /// <summary>
        /// Serializes given DOM (System.Xml.Linq.XDocument object) to a string.
        /// </summary>
        /// <param name="document">System.Xml.Linq.XDocument instance containing the DOM to be serialized.</param>
        /// <returns>Serialized representation of the DOM.</returns>
        public string Serialize(XDocument document)
        {
            return Serialize(document, DomSerializationParams.CreateDefault());
        }

        #region Private helper methods

        private static void ProcessMetaElements(XElement headElement, DomSerializationParams domSerializationParams)
        {
            ProcessMetaContentTypeElement(headElement, domSerializationParams);
            ProcessMobileSpecificMetaElements(headElement, domSerializationParams);
            ProcessMetaGeneratorElement(headElement, domSerializationParams);
        }

        private static void ProcessMetaContentTypeElement(XElement headElement, DomSerializationParams domSerializationParams)
        {
            if (!domSerializationParams.DontIncludeContentTypeMetaElement)
            {
                XElement metaContentTypeElement =
                  (from metaElement in headElement.GetChildrenByTagName("meta")
                   where "content-type".Equals(metaElement.GetAttributeValue("http-equiv", ""), StringComparison.OrdinalIgnoreCase)
                   select metaElement).FirstOrDefault();

                // remove meta 'http-equiv' element if present
                if (metaContentTypeElement != null)
                {
                    metaContentTypeElement.Remove();
                }

                // <meta charset="utf-8"/>

                // headElement.AddFirst(new XElement("meta", new XAttribute("charset", "utf-8")));
            }
        }

        private static void ProcessMobileSpecificMetaElements(XElement headElement, DomSerializationParams domSerializationParams)
        {
            XElement metaViewportElement =
              (from metaElement in headElement.GetChildrenByTagName("meta")
               where "viewport".Equals(metaElement.GetAttributeValue("name", ""), StringComparison.OrdinalIgnoreCase)
               select metaElement).FirstOrDefault();

            // remove meta 'viewport' element if present
            if (metaViewportElement != null)
            {
                metaViewportElement.Remove();
            }

        
        }

        private static void ProcessMetaGeneratorElement(XElement headElement, DomSerializationParams domSerializationParams)
        {
            if (!domSerializationParams.DontIncludeGeneratorMetaElement)
            {
                XElement metaGeneratorElement =
                  (from metaElement in headElement.GetChildrenByTagName("meta")
                   where "Generator".Equals(metaElement.GetAttributeValue("name", ""), StringComparison.OrdinalIgnoreCase)
                   select metaElement).FirstOrDefault();

                // remove meta 'generator' element if present
                if (metaGeneratorElement != null)
                {
                    metaGeneratorElement.Remove();
                }

                headElement.AddFirst(metaGeneratorElement);
            }
        }

        #endregion
    }
}
