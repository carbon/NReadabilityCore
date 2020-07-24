/*
* 
* Copyright (c) 2007-2008 MindTouch. All rights reserved.
* 
*/

using System;

namespace Sgml
{
    internal class SgmlParseException : Exception
    {
        public SgmlParseException()
        {
        }

        /// <summary>
        /// Instantiates a new instance of SgmlParseException with an error message describing the problem.
        /// </summary>
        /// <param name="message">A message describing the error that occurred</param>
        public SgmlParseException(string message)
            : base(message)
        {
        }

        public SgmlParseException(string message, Entity e)
            : base(message)
        {
            if (e != null)
            {
                EntityContext = e.Context();
            }
        }

        public SgmlParseException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        /// <summary>
        /// Contextual information detailing the entity on which the error occurred.
        /// </summary>
        public string? EntityContext { get; }
    }
}