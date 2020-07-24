/*
* 
* Copyright (c) 2007-2008 MindTouch. All rights reserved.
* 
*/

namespace Sgml
{
    /// <summary>
    /// The different types of literal text returned by the SgmlParser.
    /// </summary>
    public enum LiteralType
    {
        /// <summary>
        /// CDATA text literals.
        /// </summary>
        CDATA,

        /// <summary>
        /// SDATA entities.
        /// </summary>
        SDATA,

        /// <summary>
        /// The contents of a Processing Instruction.
        /// </summary>
        PI
    };
}