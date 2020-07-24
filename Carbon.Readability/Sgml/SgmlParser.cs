/*
* 
* Copyright (c) 2007-2008 MindTouch. All rights reserved.
* 
*/

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;

namespace Sgml
{
    // This class decodes an HTML/XML stream correctly.
    internal sealed class HtmlStream : TextReader
    {
        private readonly Stream stream;
        private readonly byte[] rawBuffer;
        private int rawPos;
        private int rawUsed;
        private Encoding m_encoding;
        private readonly Decoder m_decoder;
        private char[] m_buffer;
        private int used;
        private int pos;
        private const int BUFSIZE = 16_384;
        private const int EOF = -1;

        public HtmlStream(Stream stream, Encoding defaultEncoding)
        {
            if (defaultEncoding == null) defaultEncoding = Encoding.UTF8; // default is UTF8
            
            if (!stream.CanSeek)
            {
                stream = TransferToMemoryStream(stream);
            }

            this.stream = stream;
            rawBuffer = new byte[BUFSIZE];
            rawUsed = stream.Read(rawBuffer, 0, 4); // maximum byte order mark
            this.m_buffer = new char[BUFSIZE];

            // Check byte order marks
            this.m_decoder = AutoDetectEncoding(rawBuffer, ref rawPos, rawUsed);
            int bom = rawPos;
            if (this.m_decoder == null)
            {
                this.m_decoder = defaultEncoding.GetDecoder();
                rawUsed += stream.Read(rawBuffer, 4, BUFSIZE - 4);
                DecodeBlock();
                // Now sniff to see if there is an XML declaration or HTML <META> tag.
                Decoder sd = SniffEncoding();
                if (sd != null)
                {
                    this.m_decoder = sd;
                }
            }

            // Reset to get ready for Read()
            this.stream.Seek(0, SeekOrigin.Begin);
            this.pos = this.used = 0;

            // skip bom
            if (bom > 0)
            {
                stream.Read(this.rawBuffer, 0, bom);
            }

            this.rawPos = this.rawUsed = 0;
        }

        public Encoding Encoding => m_encoding;

        private static Stream TransferToMemoryStream(Stream s)
        {
            var ms = new MemoryStream();

            s.CopyTo(ms);
            s.Dispose();

            ms.Position = 0;
            
            return ms;           
        }

        internal void DecodeBlock()
        {
            // shift current chars to beginning.
            if (pos > 0)
            {
                if (pos < used)
                {
                    Array.Copy(m_buffer, pos, m_buffer, 0, used - pos);
                }
                used -= pos;
                pos = 0;
            }

            int len = m_decoder.GetCharCount(rawBuffer, rawPos, rawUsed - rawPos);
            int available = m_buffer.Length - used;
            if (available < len)
            {
                char[] newbuf = new char[m_buffer.Length + len];
                Array.Copy(m_buffer, pos, newbuf, 0, used - pos);
                m_buffer = newbuf;
            }
            used = pos + m_decoder.GetChars(rawBuffer, rawPos, rawUsed - rawPos, m_buffer, pos);
            rawPos = rawUsed; // consumed the whole buffer!
        }

        internal static Decoder AutoDetectEncoding(byte[] buffer, ref int index, int length)
        {
            if (4 <= (length - index))
            {
                uint w = (uint)buffer[index + 0] << 24 | (uint)buffer[index + 1] << 16 | (uint)buffer[index + 2] << 8 | (uint)buffer[index + 3];
                // see if it's a 4-byte encoding
                switch (w)
                {
                    case 0xfefffeff:
                        index += 4;
                        return new Ucs4DecoderBigEngian();

                    case 0xfffefffe:
                        index += 4;
                        return new Ucs4DecoderLittleEndian();

                    case 0x3c000000:
                        goto case 0xfefffeff;

                    case 0x0000003c:
                        goto case 0xfffefffe;
                }
                w >>= 8;
                if (w == 0xefbbbf)
                {
                    index += 3;
                    return Encoding.UTF8.GetDecoder();
                }
                w >>= 8;
                switch (w)
                {
                    case 0xfeff:
                        index += 2;
                        return UnicodeEncoding.BigEndianUnicode.GetDecoder();

                    case 0xfffe:
                        index += 2;
                        return new UnicodeEncoding(false, false).GetDecoder();

                    case 0x3c00:
                        goto case 0xfeff;

                    case 0x003c:
                        goto case 0xfffe;
                }
            }
            return null;
        }
        private int ReadChar()
        {
            // Read only up to end of current buffer then stop.
            if (pos < used) return m_buffer[pos++];
            return EOF;
        }
        private int PeekChar()
        {
            int ch = ReadChar();
            if (ch != EOF)
            {
                pos--;
            }
            return ch;
        }
        private bool SniffPattern(string pattern)
        {
            int ch = PeekChar();
            if (ch != pattern[0]) return false;
            for (int i = 0, n = pattern.Length; ch != EOF && i < n; i++)
            {
                ch = ReadChar();
                char m = pattern[i];
                if (ch != m)
                {
                    return false;
                }
            }
            return true;
        }
        private void SniffWhitespace()
        {
            char ch = (char)PeekChar();
            while (ch == ' ' || ch == '\t' || ch == '\r' || ch == '\n')
            {
                int i = pos;
                ch = (char)ReadChar();
                if (ch != ' ' && ch != '\t' && ch != '\r' && ch != '\n')
                    pos = i;
            }
        }

        private string SniffLiteral()
        {
            int quoteChar = PeekChar();
            if (quoteChar == '\'' || quoteChar == '"')
            {
                ReadChar();// consume quote char
                int i = this.pos;
                int ch = ReadChar();
                while (ch != EOF && ch != quoteChar)
                {
                    ch = ReadChar();
                }
                return (pos > i) ? new string(m_buffer, i, pos - i - 1) : "";
            }
            return null;
        }
        private string SniffAttribute(string name)
        {
            SniffWhitespace();
            string id = SniffName();
            if (string.Equals(name, id, StringComparison.OrdinalIgnoreCase))
            {
                SniffWhitespace();
                if (SniffPattern("="))
                {
                    SniffWhitespace();
                    return SniffLiteral();
                }
            }
            return null;
        }
        private string SniffAttribute(out string name)
        {
            SniffWhitespace();
            name = SniffName();
            if (name != null)
            {
                SniffWhitespace();
                if (SniffPattern("="))
                {
                    SniffWhitespace();
                    return SniffLiteral();
                }
            }
            return null;
        }
        private void SniffTerminator(string term)
        {
            int ch = ReadChar();
            int i = 0;
            int n = term.Length;
            while (i < n && ch != EOF)
            {
                if (term[i] == ch)
                {
                    i++;
                    if (i == n) break;
                }
                else
                {
                    i = 0; // reset.
                }
                ch = ReadChar();
            }
        }

        internal Decoder SniffEncoding()
        {
            Decoder decoder = null;
            if (SniffPattern("<?xml"))
            {
                string version = SniffAttribute("version");
                if (version != null)
                {
                    string encoding = SniffAttribute("encoding");
                    if (encoding != null)
                    {
                        try
                        {
                            Encoding enc = Encoding.GetEncoding(encoding);
                            if (enc != null)
                            {
                                this.m_encoding = enc;
                                return enc.GetDecoder();
                            }
                        }
                        catch (ArgumentException)
                        {
                            // oh well then.
                        }
                    }
                    SniffTerminator(">");
                }
            }
            if (decoder == null)
            {
                return SniffMeta();
            }
            return null;
        }

        internal Decoder SniffMeta()
        {
            int i = ReadChar();
            while (i != EOF)
            {
                char ch = (char)i;
                if (ch == '<')
                {
                    string name = SniffName();
                    if (name != null && name.Equals("meta", StringComparison.OrdinalIgnoreCase))
                    {
                        string? httpequiv = null;
                        string? content = null;
                        while (true)
                        {
                            string value = SniffAttribute(out name);
                            if (name == null)
                                break;

                            if (string.Equals(name, "http-equiv", StringComparison.OrdinalIgnoreCase))
                            {
                                httpequiv = value;
                            }
                            else if (string.Equals(name, "content", StringComparison.OrdinalIgnoreCase))
                            {
                                content = value;
                            }
                        }

                        if (httpequiv != null && httpequiv.Equals("content-type", StringComparison.OrdinalIgnoreCase) && content != null)
                        {
                            int j = content.IndexOf("charset");
                            if (j >= 0)
                            {
                                //charset=utf-8
                                j = content.IndexOf("=", j);
                                if (j >= 0)
                                {
                                    j++;
                                    int k = content.IndexOf(";", j);
                                    if (k < 0) k = content.Length;
                                    string charset = content.AsSpan(j, k - j).Trim().ToString();
                                    try
                                    {
                                        Encoding e = Encoding.GetEncoding(charset);
                                        this.m_encoding = e;
                                        return e.GetDecoder();
                                    }
                                    catch (ArgumentException) { }
                                }
                            }
                        }
                    }
                }
                i = ReadChar();

            }
            return null;
        }

        internal string SniffName()
        {
            int c = PeekChar();
            if (c == EOF)
                return null;
            char ch = (char)c;
            int start = pos;
            while (pos < used - 1 && (char.IsLetterOrDigit(ch) || ch == '-' || ch == '_' || ch == ':'))
                ch = m_buffer[++pos];

            if (start == pos)
                return null;

            return new string(m_buffer, start, pos - start);
        }

        [SuppressMessage("Microsoft.Performance", "CA1811", Justification = "Kept for potential future usage.")]
        internal void SkipWhitespace()
        {
            char ch = (char)PeekChar();
            while (pos < used - 1 && (ch == ' ' || ch == '\r' || ch == '\n'))
                ch = m_buffer[++pos];
        }

        [SuppressMessage("Microsoft.Performance", "CA1811", Justification = "Kept for potential future usage.")]
        internal void SkipTo(char what)
        {
            char ch = (char)PeekChar();
            while (pos < used - 1 && (ch != what))
                ch = m_buffer[++pos];
        }

        [SuppressMessage("Microsoft.Performance", "CA1811", Justification = "Kept for potential future usage.")]
        internal string ParseAttribute()
        {
            SkipTo('=');
            if (pos < used)
            {
                pos++;
                SkipWhitespace();
                if (pos < used)
                {
                    char quote = m_buffer[pos];
                    pos++;
                    int start = pos;
                    SkipTo(quote);
                    if (pos < used)
                    {
                        string result = new string(m_buffer, start, pos - start);
                        pos++;
                        return result;
                    }
                }
            }
            return null;
        }
        public override int Peek()
        {
            int result = Read();
            if (result != EOF)
            {
                pos--;
            }
            return result;
        }
        public override int Read()
        {
            if (pos == used)
            {
                rawUsed = stream.Read(rawBuffer, 0, rawBuffer.Length);
                rawPos = 0;
                if (rawUsed == 0) return EOF;
                DecodeBlock();
            }
            if (pos < used) return m_buffer[pos++];
            return -1;
        }

        public override int Read(char[] buffer, int start, int length)
        {
            if (pos == used)
            {
                rawUsed = stream.Read(rawBuffer, 0, rawBuffer.Length);
                rawPos = 0;
                if (rawUsed == 0) return -1;
                DecodeBlock();
            }
            if (pos < used)
            {
                length = Math.Min(used - pos, length);
                Array.Copy(this.m_buffer, pos, buffer, start, length);
                pos += length;
                return length;
            }
            return 0;
        }

        public override int ReadBlock(char[] data, int index, int count)
        {
            return Read(data, index, count);
        }

        // Read up to end of line, or full buffer, whichever comes first.
        public int ReadLine(char[] buffer, int start, int length)
        {
            int i = 0;
            int ch = ReadChar();
            while (ch != EOF)
            {
                buffer[i + start] = (char)ch;
                i++;
                if (i + start == length)
                    break; // buffer is full

                if (ch == '\r')
                {
                    if (PeekChar() == '\n')
                    {
                        ch = ReadChar();
                        buffer[i + start] = (char)ch;
                        i++;
                    }
                    break;
                }
                else if (ch == '\n')
                {
                    break;
                }
                ch = ReadChar();
            }
            return i;
        }

      
        public override void Close()
        {
            stream.Close();
        }
    }

    internal abstract class Ucs4Decoder : Decoder
    {
        internal byte[] temp = new byte[4];
        internal int tempBytes = 0;
        public override int GetCharCount(byte[] bytes, int index, int count)
        {
            return (count + tempBytes) / 4;
        }
        internal abstract int GetFullChars(byte[] bytes, int byteIndex, int byteCount, char[] chars, int charIndex);
        public override int GetChars(byte[] bytes, int byteIndex, int byteCount, char[] chars, int charIndex)
        {
            int i = tempBytes;

            if (tempBytes > 0)
            {
                for (; i < 4; i++)
                {
                    temp[i] = bytes[byteIndex];
                    byteIndex++;
                    byteCount--;
                }
                i = 1;
                GetFullChars(temp, 0, 4, chars, charIndex);
                charIndex++;
            }
            else
                i = 0;
            i = GetFullChars(bytes, byteIndex, byteCount, chars, charIndex) + i;

            int j = (tempBytes + byteCount) % 4;
            byteCount += byteIndex;
            byteIndex = byteCount - j;
            tempBytes = 0;

            if (byteIndex >= 0)
                for (; byteIndex < byteCount; byteIndex++)
                {
                    temp[tempBytes] = bytes[byteIndex];
                    tempBytes++;
                }
            return i;
        }
        internal static char UnicodeToUTF16(UInt32 code)
        {
            byte lowerByte, higherByte;
            lowerByte = (byte)(0xD7C0 + (code >> 10));
            higherByte = (byte)(0xDC00 | code & 0x3ff);
            return ((char)((higherByte << 8) | lowerByte));
        }
    }

    internal class Ucs4DecoderBigEngian : Ucs4Decoder
    {
        internal override int GetFullChars(byte[] bytes, int byteIndex, int byteCount, char[] chars, int charIndex)
        {
            UInt32 code;
            int i, j;
            byteCount += byteIndex;
            for (i = byteIndex, j = charIndex; i + 3 < byteCount;)
            {
                code = (UInt32)(((bytes[i + 3]) << 24) | (bytes[i + 2] << 16) | (bytes[i + 1] << 8) | (bytes[i]));
                if (code > 0x10FFFF)
                {
                    throw new SgmlParseException(string.Format(CultureInfo.CurrentUICulture, "Invalid character 0x{0:x} in encoding", code));
                }
                else if (code > 0xFFFF)
                {
                    chars[j] = UnicodeToUTF16(code);
                    j++;
                }
                else
                {
                    if (code >= 0xD800 && code <= 0xDFFF)
                    {
                        throw new SgmlParseException(string.Format(CultureInfo.CurrentUICulture, "Invalid character 0x{0:x} in encoding", code));
                    }
                    else
                    {
                        chars[j] = (char)code;
                    }
                }
                j++;
                i += 4;
            }
            return j - charIndex;
        }
    }

    internal sealed class Ucs4DecoderLittleEndian : Ucs4Decoder
    {
        internal override int GetFullChars(byte[] bytes, int byteIndex, int byteCount, char[] chars, int charIndex)
        {
            uint code;
            int i, j;
            byteCount += byteIndex;
            for (i = byteIndex, j = charIndex; i + 3 < byteCount;)
            {
                code = (uint)(((bytes[i]) << 24) | (bytes[i + 1] << 16) | (bytes[i + 2] << 8) | (bytes[i + 3]));
                if (code > 0x10FFFF)
                {
                    throw new SgmlParseException(string.Format(CultureInfo.CurrentUICulture, "Invalid character 0x{0:x} in encoding", code));
                }
                else if (code > 0xFFFF)
                {
                    chars[j] = UnicodeToUTF16(code);
                    j++;
                }
                else
                {
                    if (code >= 0xD800 && code <= 0xDFFF)
                    {
                        throw new SgmlParseException(string.Format(CultureInfo.CurrentUICulture, "Invalid character 0x{0:x} in encoding", code));
                    }
                    else
                    {
                        chars[j] = (char)code;
                    }
                }
                j++;
                i += 4;
            }
            return j - charIndex;
        }
    }

    /// <summary>
    /// An element declaration in a DTD.
    /// </summary>
    public sealed class ElementDecl
    {
        private readonly string m_name;
        private readonly bool m_startTagOptional;
        private readonly bool m_endTagOptional;
        private readonly ContentModel m_contentModel;
        private readonly string[] m_inclusions;
        private readonly string[] m_exclusions;
        private Dictionary<string, AttDef> m_attList;

        /// <summary>
        /// Initialises a new element declaration instance.
        /// </summary>
        /// <param name="name">The name of the element.</param>
        /// <param name="sto">Whether the start tag is optional.</param>
        /// <param name="eto">Whether the end tag is optional.</param>
        /// <param name="cm">The <see cref="ContentModel"/> of the element.</param>
        /// <param name="inclusions"></param>
        /// <param name="exclusions"></param>
        public ElementDecl(string name, bool sto, bool eto, ContentModel cm, string[] inclusions, string[] exclusions)
        {
            m_name = name;
            m_startTagOptional = sto;
            m_endTagOptional = eto;
            m_contentModel = cm;
            m_inclusions = inclusions;
            m_exclusions = exclusions;
        }

        /// <summary>
        /// The element name.
        /// </summary>
        public string Name
        {
            get
            {
                return m_name;
            }
        }

        /// <summary>
        /// The <see cref="Sgml.ContentModel"/> of the element declaration.
        /// </summary>
        public ContentModel ContentModel
        {
            get
            {
                return m_contentModel;
            }
        }

        /// <summary>
        /// Whether the end tag of the element is optional.
        /// </summary>
        /// <value>true if the end tag of the element is optional, otherwise false.</value>
        public bool EndTagOptional
        {
            get
            {
                return m_endTagOptional;
            }
        }

        /// <summary>
        /// Whether the start tag of the element is optional.
        /// </summary>
        /// <value>true if the start tag of the element is optional, otherwise false.</value>
        public bool StartTagOptional
        {
            get
            {
                return m_startTagOptional;
            }
        }

        /// <summary>
        /// Finds the attribute definition with the specified name.
        /// </summary>
        /// <param name="name">The name of the <see cref="AttDef"/> to find.</param>
        /// <returns>The <see cref="AttDef"/> with the specified name.</returns>
        /// <exception cref="InvalidOperationException">If the attribute list has not yet been initialised.</exception>
        public AttDef FindAttribute(string name)
        {
            if (m_attList == null)
                throw new InvalidOperationException("The attribute list for the element declaration has not been initialised.");

            m_attList.TryGetValue(name.ToUpperInvariant(), out AttDef a);
            return a;
        }

        /// <summary>
        /// Adds attribute definitions to the element declaration.
        /// </summary>
        /// <param name="list">The list of attribute definitions to add.</param>
        public void AddAttDefs(Dictionary<string, AttDef> list)
        {
            if (list == null)
                throw new ArgumentNullException("list");

            if (m_attList == null)
            {
                m_attList = list;
            }
            else
            {
                foreach (AttDef a in list.Values)
                {
                    if (!m_attList.ContainsKey(a.Name))
                    {
                        m_attList.Add(a.Name, a);
                    }
                }
            }
        }

        /// <summary>
        /// Tests whether this element can contain another specified element.
        /// </summary>
        /// <param name="name">The name of the element to check for.</param>
        /// <param name="dtd">The DTD to use to do the check.</param>
        /// <returns>True if the specified element can be contained by this element.</returns>
        public bool CanContain(string name, SgmlDtd dtd)
        {
            // return true if this element is allowed to contain the given element.
            if (m_exclusions != null)
            {
                foreach (string s in m_exclusions)
                {
                    if (string.Equals(s, name, StringComparison.OrdinalIgnoreCase))
                        return false;
                }
            }

            if (m_inclusions != null)
            {
                foreach (string s in m_inclusions)
                {
                    if (string.Equals(s, name, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
            return m_contentModel.CanContain(name, dtd);
        }
    }

    /// <summary>
    /// Where nested subelements cannot occur within an element, its contents can be declared to consist of one of the types of declared content contained in this enumeration.
    /// </summary>
    public enum DeclaredContent
    {
        /// <summary>
        /// Not defined.
        /// </summary>
        Default,

        /// <summary>
        /// Character data (CDATA), which contains only valid SGML characters.
        /// </summary>
        [SuppressMessage("Microsoft.Naming", "CA1705", Justification = "This capitalisation is appropriate since the value it represents has all upper-case capitalisation.")]
        CDATA,

        /// <summary>
        /// Replaceable character data (RCDATA), which can contain text, character references and/or general entity references that resolve to character data.
        /// </summary>
        [SuppressMessage("Microsoft.Naming", "CA1705", Justification = "This capitalisation is appropriate since the value it represents has all upper-case capitalisation.")]
        RCDATA,

        /// <summary>
        /// Empty element (EMPTY), i.e. having no contents, or contents that can be generated by the program.
        /// </summary>
        [SuppressMessage("Microsoft.Naming", "CA1705", Justification = "This capitalisation is appropriate since the value it represents has all upper-case capitalisation.")]
        EMPTY
    }

    /// <summary>
    /// Defines the content model for an element.
    /// </summary>
    public class ContentModel
    {
        private DeclaredContent m_declaredContent;
        private int m_currentDepth;
        private Group m_model;

        /// <summary>
        /// Initialises a new instance of the <see cref="ContentModel"/> class.
        /// </summary>
        public ContentModel()
        {
            m_model = new Group(null);
        }

        /// <summary>
        /// The number of groups on the stack.
        /// </summary>
        public int CurrentDepth
        {
            get
            {
                return m_currentDepth;
            }
        }

        /// <summary>
        /// The allowed child content, specifying if nested children are not allowed and if so, what content is allowed.
        /// </summary>
        public DeclaredContent DeclaredContent
        {
            get
            {
                return m_declaredContent;
            }
        }

        /// <summary>
        /// Begins processing of a nested model group.
        /// </summary>
        public void PushGroup()
        {
            m_model = new Group(m_model);
            m_currentDepth++;
        }

        /// <summary>
        /// Finishes processing of a nested model group.
        /// </summary>
        /// <returns>The current depth of the group nesting, or -1 if there are no more groups to pop.</returns>
        public int PopGroup()
        {
            if (m_currentDepth == 0)
                return -1;

            m_currentDepth--;
            m_model.Parent.AddGroup(m_model);
            m_model = m_model.Parent;
            return m_currentDepth;
        }

        /// <summary>
        /// Adds a new symbol to the current group's members.
        /// </summary>
        /// <param name="sym">The symbol to add.</param>
        public void AddSymbol(string sym)
        {
            m_model.AddSymbol(sym);
        }

        /// <summary>
        /// Adds a connector onto the member list for the current group.
        /// </summary>
        /// <param name="c">The connector character to add.</param>
        /// <exception cref="SgmlParseException">
        /// If the content is not mixed and has no members yet, or if the group type has been set and the
        /// connector does not match the group type.
        /// </exception>
        public void AddConnector(char c)
        {
            m_model.AddConnector(c);
        }

        /// <summary>
        /// Adds an occurrence character for the current model group, setting it's <see cref="Occurrence"/> value.
        /// </summary>
        /// <param name="c">The occurrence character.</param>
        public void AddOccurrence(char c)
        {
            m_model.AddOccurrence(c);
        }

        /// <summary>
        /// Sets the contained content for the content model.
        /// </summary>
        /// <param name="dc">The text specified the permissible declared child content.</param>
        public void SetDeclaredContent(string dc)
        {
            // TODO: Validate that this can never combine with nexted groups?
            this.m_declaredContent = dc switch
            {
                "EMPTY" => DeclaredContent.EMPTY,
                "RCDATA" => DeclaredContent.RCDATA,
                "CDATA" => DeclaredContent.CDATA,
                _ => throw new SgmlParseException(string.Format(CultureInfo.CurrentUICulture, "Declared content type '{0}' is not supported", dc)),
            };
        }

        /// <summary>
        /// Checks whether an element using this group can contain a specified element.
        /// </summary>
        /// <param name="name">The name of the element to look for.</param>
        /// <param name="dtd">The DTD to use during the checking.</param>
        /// <returns>true if an element using this group can contain the element, otherwise false.</returns>
        public bool CanContain(string name, SgmlDtd dtd)
        {
            if (m_declaredContent != DeclaredContent.Default)
                return false; // empty or text only node.

            return m_model.CanContain(name, dtd);
        }
    }

    /// <summary>
    /// The type of the content model group, defining the order in which child elements can occur.
    /// </summary>
    public enum GroupType
    {
        /// <summary>
        /// No model group.
        /// </summary>
        None,

        /// <summary>
        /// All elements must occur, in any order.
        /// </summary>
        And,

        /// <summary>
        /// One (and only one) must occur.
        /// </summary>
        Or,

        /// <summary>
        /// All element must occur, in the specified order.
        /// </summary>
        Sequence
    };

    /// <summary>
    /// Qualifies the occurrence of a child element within a content model group.
    /// </summary>
    public enum Occurrence
    {
        /// <summary>
        /// The element is required and must occur only once.
        /// </summary>
        Required,

        /// <summary>
        /// The element is optional and must occur once at most.
        /// </summary>
        Optional,

        /// <summary>
        /// The element is optional and can be repeated.
        /// </summary>
        ZeroOrMore,

        /// <summary>
        /// The element must occur at least once or more times.
        /// </summary>
        OneOrMore
    }

    /// <summary>
    /// Defines a group of elements nested within another element.
    /// </summary>
    public class Group
    {
        private readonly Group m_parent;
        private readonly ArrayList Members;
        private GroupType m_groupType;
        private Occurrence m_occurrence;
        private bool Mixed;

        /// <summary>
        /// The <see cref="Occurrence"/> of this group.
        /// </summary>
        public Occurrence Occurrence => m_occurrence;

        /// <summary>
        /// Checks whether the group contains only text.
        /// </summary>
        /// <value>true if the group is of mixed content and has no members, otherwise false.</value>
        public bool TextOnly => this.Mixed && Members.Count == 0;

        /// <summary>
        /// The parent group of this group.
        /// </summary>
        public Group Parent => m_parent;

        /// <summary>
        /// Initialises a new Content Model Group.
        /// </summary>
        /// <param name="parent">The parent model group.</param>
        public Group(Group parent)
        {
            m_parent = parent;
            Members = new ArrayList();
            m_groupType = GroupType.None;
            m_occurrence = Occurrence.Required;
        }

        /// <summary>
        /// Adds a new child model group to the end of the group's members.
        /// </summary>
        /// <param name="g">The model group to add.</param>
        public void AddGroup(Group g)
        {
            Members.Add(g);
        }

        /// <summary>
        /// Adds a new symbol to the group's members.
        /// </summary>
        /// <param name="sym">The symbol to add.</param>
        public void AddSymbol(string sym)
        {
            if (string.Equals(sym, "#PCDATA", StringComparison.OrdinalIgnoreCase))
            {
                Mixed = true;
            }
            else
            {
                Members.Add(sym);
            }
        }

        /// <summary>
        /// Adds a connector onto the member list.
        /// </summary>
        /// <param name="c">The connector character to add.</param>
        /// <exception cref="SgmlParseException">
        /// If the content is not mixed and has no members yet, or if the group type has been set and the
        /// connector does not match the group type.
        /// </exception>
        public void AddConnector(char c)
        {
            if (!Mixed && Members.Count == 0)
            {
                throw new SgmlParseException(string.Format(CultureInfo.CurrentUICulture, "Missing token before connector '{0}'.", c));
            }

            GroupType gt = GroupType.None;
            switch (c)
            {
                case ',':
                    gt = GroupType.Sequence;
                    break;
                case '|':
                    gt = GroupType.Or;
                    break;
                case '&':
                    gt = GroupType.And;
                    break;
            }

            if (this.m_groupType != GroupType.None && this.m_groupType != gt)
            {
                throw new SgmlParseException(string.Format(CultureInfo.CurrentUICulture, "Connector '{0}' is inconsistent with {1} group.", c, m_groupType.ToString()));
            }

            m_groupType = gt;
        }

        /// <summary>
        /// Adds an occurrence character for this group, setting it's <see cref="Occurrence"/> value.
        /// </summary>
        /// <param name="c">The occurrence character.</param>
        public void AddOccurrence(char c)
        {
            Occurrence o = Occurrence.Required;
            switch (c)
            {
                case '?':
                    o = Occurrence.Optional;
                    break;
                case '+':
                    o = Occurrence.OneOrMore;
                    break;
                case '*':
                    o = Occurrence.ZeroOrMore;
                    break;
            }

            m_occurrence = o;
        }

        /// <summary>
        /// Checks whether an element using this group can contain a specified element.
        /// </summary>
        /// <param name="name">The name of the element to look for.</param>
        /// <param name="dtd">The DTD to use during the checking.</param>
        /// <returns>true if an element using this group can contain the element, otherwise false.</returns>
        /// <remarks>
        /// Rough approximation - this is really assuming an "Or" group
        /// </remarks>
        public bool CanContain(string name, SgmlDtd dtd)
        {
            // Do a simple search of members.
            foreach (object obj in Members)
            {
                if (obj is string text)
                {
                    if (string.Equals(text, name, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
            // didn't find it, so do a more expensive search over child elements
            // that have optional start tags and over child groups.
            foreach (object obj in Members)
            {
                if (obj is string s)
                {
                    ElementDecl e = dtd.FindElement(s);
                    if (e != null)
                    {
                        if (e.StartTagOptional)
                        {
                            // tricky case, the start tag is optional so element may be
                            // allowed inside this guy!
                            if (e.CanContain(name, dtd))
                                return true;
                        }
                    }
                }
                else
                {
                    Group m = (Group)obj;
                    if (m.CanContain(name, dtd))
                        return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Defines the different possible attribute types.
    /// </summary>
    public enum AttributeType
    {
        /// <summary>
        /// Attribute type not specified.
        /// </summary>
        Default,

        /// <summary>
        /// The attribute contains text (with no markup).
        /// </summary>
        CDATA,

        /// <summary>
        /// The attribute contains an entity declared in a DTD.
        /// </summary>
        ENTITY,

        /// <summary>
        /// The attribute contains a number of entities declared in a DTD.
        /// </summary>
        ENTITIES,

        /// <summary>
        /// The attribute is an id attribute uniquely identifie the element it appears on.
        /// </summary>
        ID,

        /// <summary>
        /// The attribute value can be any declared subdocument or data entity name.
        /// </summary>
        IDREF,

        /// <summary>
        /// The attribute value is a list of (space separated) declared subdocument or data entity names.
        /// </summary>
        IDREFS,

        /// <summary>
        /// The attribute value is a SGML Name.
        /// </summary>
        NAME,

        /// <summary>
        /// The attribute value is a list of (space separated) SGML Names.
        /// </summary>
        NAMES,

        /// <summary>
        /// The attribute value is an XML name token (i.e. contains only name characters, but in this case with digits and other valid name characters accepted as the first character).
        /// </summary>
        NMTOKEN,

        /// <summary>
        /// The attribute value is a list of (space separated) XML NMTokens.
        /// </summary>
        NMTOKENS,

        /// <summary>
        /// The attribute value is a number.
        /// </summary>
        NUMBER,

        /// <summary>
        /// The attribute value is a list of (space separated) numbers.
        /// </summary>
        NUMBERS,

        /// <summary>
        /// The attribute value is a number token (i.e. a name that starts with a number).
        /// </summary>
        NUTOKEN,

        /// <summary>
        /// The attribute value is a list of number tokens.
        /// </summary>
        NUTOKENS,

        /// <summary>
        /// Attribute value is a member of the bracketed list of notation names that qualifies this reserved name.
        /// </summary>
        NOTATION,

        /// <summary>
        /// The attribute value is one of a set of allowed names.
        /// </summary>
        ENUMERATION
    }

    /// <summary>
    /// Defines the different constraints on an attribute's presence on an element.
    /// </summary>
    public enum AttributePresence
    {
        /// <summary>
        /// The attribute has a default value, and its presence is optional.
        /// </summary>
        Default,

        /// <summary>
        /// The attribute has a fixed value, if present.
        /// </summary>
        Fixed,

        /// <summary>
        /// The attribute must always be present on every element.
        /// </summary>
        Required,

        /// <summary>
        /// The element is optional.
        /// </summary>
        Implied
    }

    /// <summary>
    /// An attribute definition in a DTD.
    /// </summary>
    public class AttDef
    {
        private readonly string m_name;
        private AttributeType m_type;
        private string[] m_enumValues;
        private string m_default;
        private AttributePresence m_presence;

        /// <summary>
        /// Initialises a new instance of the <see cref="AttDef"/> class.
        /// </summary>
        /// <param name="name">The name of the attribute.</param>
        public AttDef(string name)
        {
            m_name = name;
        }

        /// <summary>
        /// The name of the attribute declared by this attribute definition.
        /// </summary>
        public string Name
        {
            get
            {
                return m_name;
            }
        }

        /// <summary>
        /// Gets of sets the default value of the attribute.
        /// </summary>
        public string Default
        {
            get
            {
                return m_default;
            }
            set
            {
                m_default = value;
            }
        }

        /// <summary>
        /// The constraints on the attribute's presence on an element.
        /// </summary>
        public AttributePresence AttributePresence => m_presence;

        /// <summary>
        /// Gets or sets the possible enumerated values for the attribute.
        /// </summary>
        public string[] EnumValues => m_enumValues;

        /// <summary>
        /// Sets the attribute definition to have an enumerated value.
        /// </summary>
        /// <param name="enumValues">The possible values in the enumeration.</param>
        /// <param name="type">The type to set the attribute to.</param>
        /// <exception cref="ArgumentException">If the type parameter is not either <see cref="AttributeType.ENUMERATION"/> or <see cref="AttributeType.NOTATION"/>.</exception>
        public void SetEnumeratedType(string[] enumValues, AttributeType type)
        {
            if (type != AttributeType.ENUMERATION && type != AttributeType.NOTATION)
                throw new ArgumentException(string.Format(CultureInfo.CurrentUICulture, "AttributeType {0} is not valid for an attribute definition with an enumerated value.", type));

            m_enumValues = enumValues;
            m_type = type;
        }

        /// <summary>
        /// The <see cref="AttributeType"/> of the attribute declaration.
        /// </summary>
        public AttributeType Type
        {
            get
            {
                return m_type;
            }
        }

        /// <summary>
        /// Sets the type of the attribute definition.
        /// </summary>
        /// <param name="type">The string representation of the attribute type, corresponding to the values in the <see cref="AttributeType"/> enumeration.</param>
        public void SetType(string type)
        {
            m_type = type switch
            {
                "CDATA" => AttributeType.CDATA,
                "ENTITY" => AttributeType.ENTITY,
                "ENTITIES" => AttributeType.ENTITIES,
                "ID" => AttributeType.ID,
                "IDREF" => AttributeType.IDREF,
                "IDREFS" => AttributeType.IDREFS,
                "NAME" => AttributeType.NAME,
                "NAMES" => AttributeType.NAMES,
                "NMTOKEN" => AttributeType.NMTOKEN,
                "NMTOKENS" => AttributeType.NMTOKENS,
                "NUMBER" => AttributeType.NUMBER,
                "NUMBERS" => AttributeType.NUMBERS,
                "NUTOKEN" => AttributeType.NUTOKEN,
                "NUTOKENS" => AttributeType.NUTOKENS,
                _ => throw new SgmlParseException(string.Format(CultureInfo.CurrentUICulture, "Attribute type '{0}' is not supported", type)),
            };
        }

        /// <summary>
        /// Sets the attribute presence declaration.
        /// </summary>
        /// <param name="token">The string representation of the attribute presence, corresponding to one of the values in the <see cref="AttributePresence"/> enumeration.</param>
        /// <returns>true if the attribute presence implies the element has a default value.</returns>
        public bool SetPresence(string token)
        {
            bool hasDefault = true;
            if (string.Equals(token, "FIXED", StringComparison.OrdinalIgnoreCase))
            {
                m_presence = AttributePresence.Fixed;
            }
            else if (string.Equals(token, "REQUIRED", StringComparison.OrdinalIgnoreCase))
            {
                m_presence = AttributePresence.Required;
                hasDefault = false;
            }
            else if (string.Equals(token, "IMPLIED", StringComparison.OrdinalIgnoreCase))
            {
                m_presence = AttributePresence.Implied;
                hasDefault = false;
            }
            else
            {
                throw new SgmlParseException(string.Format(CultureInfo.CurrentUICulture, "Attribute value '{0}' not supported", token));
            }

            return hasDefault;
        }
    }

    /// <summary>
    /// Provides DTD parsing and support for the SgmlParser framework.
    /// </summary>
    public sealed class SgmlDtd
    {
        private readonly string m_name;

        private readonly Dictionary<string, ElementDecl> m_elements;
        private readonly Dictionary<string, Entity> m_pentities;
        private readonly Dictionary<string, Entity> m_entities;
        private readonly StringBuilder m_sb;
        private Entity m_current;

        /// <summary>
        /// Initialises a new instance of the <see cref="SgmlDtd"/> class.
        /// </summary>
        /// <param name="name">The name of the DTD.</param>
        /// <param name="nt">The <see cref="XmlNameTable"/> is NOT used.</param>
        public SgmlDtd(string name, XmlNameTable nt)
        {
            this.m_name = name;
            this.m_elements = new Dictionary<string, ElementDecl>();
            this.m_pentities = new Dictionary<string, Entity>();
            this.m_entities = new Dictionary<string, Entity>();
            this.m_sb = new StringBuilder();
        }

        /// <summary>
        /// The name of the DTD.
        /// </summary>
        public string Name => m_name;

        /// <summary>
        /// Gets the XmlNameTable associated with this implementation.
        /// </summary>
        /// <value>The XmlNameTable enabling you to get the atomized version of a string within the node.</value>
        public XmlNameTable NameTable => null;

        /// <summary>
        /// Parses a DTD and creates a <see cref="SgmlDtd"/> instance that encapsulates the DTD.
        /// </summary>
        /// <param name="baseUri">The base URI of the DTD.</param>
        /// <param name="name">The name of the DTD.</param>
        /// <param name="pubid"></param>
        /// <param name="url"></param>
        /// <param name="subset"></param>
        /// <param name="proxy"></param>
        /// <param name="nt">The <see cref="XmlNameTable"/> is NOT used.</param>
        /// <returns>A new <see cref="SgmlDtd"/> instance that encapsulates the DTD.</returns>
        public static SgmlDtd Parse(Uri baseUri, string name, string pubid, string url, string subset, string proxy, XmlNameTable nt)
        {
            SgmlDtd dtd = new SgmlDtd(name, nt);
            if (!string.IsNullOrEmpty(url))
            {
                dtd.PushEntity(baseUri, new Entity(dtd.Name, pubid, url, proxy));
            }

            if (!string.IsNullOrEmpty(subset))
            {
                dtd.PushEntity(baseUri, new Entity(name, subset));
            }

            try
            {
                dtd.Parse();
            }
            catch (ApplicationException e)
            {
                throw new SgmlParseException(e.Message + dtd.m_current.Context());
            }

            return dtd;
        }

        /// <summary>
        /// Parses a DTD and creates a <see cref="SgmlDtd"/> instance that encapsulates the DTD.
        /// </summary>
        /// <param name="baseUri">The base URI of the DTD.</param>
        /// <param name="name">The name of the DTD.</param>
        /// <param name="input">The reader to load the DTD from.</param>
        /// <param name="subset"></param>
        /// <param name="proxy">The proxy server to use when loading resources.</param>
        /// <param name="nt">The <see cref="XmlNameTable"/> is NOT used.</param>
        /// <returns>A new <see cref="SgmlDtd"/> instance that encapsulates the DTD.</returns>
        public static SgmlDtd Parse(Uri baseUri, string name, TextReader input, string subset, string proxy, XmlNameTable nt)
        {
            SgmlDtd dtd = new SgmlDtd(name, nt);
            dtd.PushEntity(baseUri, new Entity(dtd.Name, baseUri, input, proxy));
            if (!string.IsNullOrEmpty(subset))
            {
                dtd.PushEntity(baseUri, new Entity(name, subset));
            }

            try
            {
                dtd.Parse();
            }
            catch (ApplicationException e)
            {
                throw new SgmlParseException(e.Message + dtd.m_current.Context());
            }

            return dtd;
        }

        /// <summary>
        /// Finds an entity in the DTD with the specified name.
        /// </summary>
        /// <param name="name">The name of the <see cref="Entity"/> to find.</param>
        /// <returns>The specified Entity from the DTD.</returns>
        public Entity? FindEntity(string name)
        {
            return m_entities.TryGetValue(name, out Entity? e) ? e : null;
        }

        /// <summary>
        /// Finds an element declaration in the DTD with the specified name.
        /// </summary>
        /// <param name="name">The name of the <see cref="ElementDecl"/> to find and return.</param>
        /// <returns>The <see cref="ElementDecl"/> matching the specified name.</returns>
        public ElementDecl? FindElement(string name)
        {
            return m_elements.TryGetValue(name.ToUpperInvariant(), out ElementDecl? el) ? el : null;
        }

        //-------------------------------- Parser -------------------------
        private void PushEntity(Uri? baseUri, Entity e)
        {
            e.Open(this.m_current, baseUri);
            this.m_current = e;
            this.m_current.ReadChar();
        }

        private void PopEntity()
        {
            if (this.m_current != null) this.m_current.Close();
            if (this.m_current.Parent != null)
            {
                this.m_current = this.m_current.Parent;
            }
            else
            {
                this.m_current = null;
            }
        }

        private void Parse()
        {
            char ch = this.m_current.Lastchar;
            while (true)
            {
                switch (ch)
                {
                    case Entity.EOF:
                        PopEntity();
                        if (this.m_current == null)
                            return;
                        ch = this.m_current.Lastchar;
                        break;
                    case ' ':
                    case '\n':
                    case '\r':
                    case '\t':
                        ch = this.m_current.ReadChar();
                        break;
                    case '<':
                        ParseMarkup();
                        ch = this.m_current.ReadChar();
                        break;
                    case '%':
                        Entity e = ParseParameterEntity(SgmlDtd.WhiteSpace);
                        try
                        {
                            PushEntity(this.m_current.ResolvedUri, e);
                        }
                        catch (Exception ex)
                        {
                            // BUG: need an error log.
                            Console.WriteLine(ex.Message + this.m_current.Context());
                        }
                        ch = this.m_current.Lastchar;
                        break;
                    default:
                        this.m_current.Error("Unexpected character '{0}'", ch);
                        break;
                }
            }
        }

        void ParseMarkup()
        {
            char ch = this.m_current.ReadChar();
            if (ch != '!')
            {
                this.m_current.Error("Found '{0}', but expecing declaration starting with '<!'");
                return;
            }
            ch = this.m_current.ReadChar();
            if (ch == '-')
            {
                ch = this.m_current.ReadChar();
                if (ch != '-') this.m_current.Error("Expecting comment '<!--' but found {0}", ch);
                this.m_current.ScanToEnd(this.m_sb, "Comment", "-->");
            }
            else if (ch == '[')
            {
                ParseMarkedSection();
            }
            else
            {
                string token = this.m_current.ScanToken(this.m_sb, SgmlDtd.WhiteSpace, true);
                switch (token)
                {
                    case "ENTITY":
                        ParseEntity();
                        break;
                    case "ELEMENT":
                        ParseElementDecl();
                        break;
                    case "ATTLIST":
                        ParseAttList();
                        break;
                    default:
                        this.m_current.Error("Invalid declaration '<!{0}'.  Expecting 'ENTITY', 'ELEMENT' or 'ATTLIST'.", token);
                        break;
                }
            }
        }

        char ParseDeclComments()
        {
            char ch = this.m_current.Lastchar;
            while (ch == '-')
            {
                ch = ParseDeclComment(true);
            }
            return ch;
        }

        char ParseDeclComment(bool full)
        {
            int start = this.m_current.Line;
            // -^-...--
            // This method scans over a comment inside a markup declaration.
            char ch = this.m_current.ReadChar();
            if (full && ch != '-') this.m_current.Error("Expecting comment delimiter '--' but found {0}", ch);
            this.m_current.ScanToEnd(this.m_sb, "Markup Comment", "--");
            return this.m_current.SkipWhitespace();
        }

        void ParseMarkedSection()
        {
            // <![^ name [ ... ]]>
            this.m_current.ReadChar(); // move to next char.
            string name = ScanName("[");
            if (string.Equals(name, "INCLUDE", StringComparison.OrdinalIgnoreCase))
            {
                ParseIncludeSection();
            }
            else if (string.Equals(name, "IGNORE", StringComparison.OrdinalIgnoreCase))
            {
                ParseIgnoreSection();
            }
            else
            {
                this.m_current.Error("Unsupported marked section type '{0}'", name);
            }
        }

        [SuppressMessage("Microsoft.Performance", "CA1822", Justification = "This is not yet implemented and will use 'this' in the future.")]
        [SuppressMessage("Microsoft.Globalization", "CA1303", Justification = "The use of a literal here is only due to this not yet being implemented.")]
        private void ParseIncludeSection()
        {
            throw new NotImplementedException("Include Section");
        }

        void ParseIgnoreSection()
        {
            int start = this.m_current.Line;
            // <!-^-...-->
            char ch = this.m_current.SkipWhitespace();
            if (ch != '[') this.m_current.Error("Expecting '[' but found {0}", ch);
            this.m_current.ScanToEnd(this.m_sb, "Conditional Section", "]]>");
        }

        string ScanName(string term)
        {
            // skip whitespace, scan name (which may be parameter entity reference
            // which is then expanded to a name)
            char ch = this.m_current.SkipWhitespace();
            if (ch == '%')
            {
                Entity e = ParseParameterEntity(term);
                ch = this.m_current.Lastchar;
                // bugbug - need to support external and nested parameter entities
                if (!e.IsInternal) throw new NotSupportedException("External parameter entity resolution");
                return e.Literal.Trim();
            }
            else
            {
                return this.m_current.ScanToken(this.m_sb, term, true);
            }
        }

        private Entity ParseParameterEntity(string term)
        {
            // almost the same as this.current.ScanToken, except we also terminate on ';'
            char ch = this.m_current.ReadChar();
            string name = this.m_current.ScanToken(this.m_sb, ";" + term, false);
            if (this.m_current.Lastchar == ';')
                this.m_current.ReadChar();
            Entity e = GetParameterEntity(name);
            return e;
        }

        private Entity GetParameterEntity(string name)
        {
            m_pentities.TryGetValue(name, out Entity e);
            if (e is null)
                this.m_current.Error("Reference to undefined parameter entity '{0}'", name);

            return e;
        }

        /// <summary>
        /// Returns a dictionary for looking up entities by their <see cref="Entity.Literal"/> value.
        /// </summary>
        /// <returns>A dictionary for looking up entities by their <see cref="Entity.Literal"/> value.</returns>
        [SuppressMessage("Microsoft.Design", "CA1024", Justification = "This method creates and copies a dictionary, so exposing it as a property is not appropriate.")]
        public Dictionary<string, Entity> GetEntitiesLiteralNameLookup()
        {
            Dictionary<string, Entity> hashtable = new Dictionary<string, Entity>();
            foreach (Entity entity in this.m_entities.Values)
                hashtable[entity.Literal] = entity;

            return hashtable;
        }

        private const string WhiteSpace = " \r\n\t";

        private void ParseEntity()
        {
            char ch = this.m_current.SkipWhitespace();
            bool pe = (ch == '%');
            if (pe)
            {
                // parameter entity.
                this.m_current.ReadChar(); // move to next char
                ch = this.m_current.SkipWhitespace();
            }
            string name = this.m_current.ScanToken(this.m_sb, SgmlDtd.WhiteSpace, true);
            ch = this.m_current.SkipWhitespace();
            Entity e;
            if (ch == '"' || ch == '\'')
            {
                string literal = this.m_current.ScanLiteral(this.m_sb, ch);
                e = new Entity(name, literal);
            }
            else
            {
                string pubid = null;
                string extid;
                string tok = this.m_current.ScanToken(this.m_sb, SgmlDtd.WhiteSpace, true);
                if (Entity.IsLiteralType(tok))
                {
                    ch = this.m_current.SkipWhitespace();
                    string literal = this.m_current.ScanLiteral(this.m_sb, ch);
                    e = new Entity(name, literal);
                    e.SetLiteralType(tok);
                }
                else
                {
                    extid = tok;
                    if (string.Equals(extid, "PUBLIC", StringComparison.OrdinalIgnoreCase))
                    {
                        ch = this.m_current.SkipWhitespace();
                        if (ch == '"' || ch == '\'')
                        {
                            pubid = this.m_current.ScanLiteral(this.m_sb, ch);
                        }
                        else
                        {
                            this.m_current.Error("Expecting public identifier literal but found '{0}'", ch);
                        }
                    }
                    else if (!string.Equals(extid, "SYSTEM", StringComparison.OrdinalIgnoreCase))
                    {
                        this.m_current.Error("Invalid external identifier '{0}'.  Expecing 'PUBLIC' or 'SYSTEM'.", extid);
                    }
                    string? uri = null;
                    ch = this.m_current.SkipWhitespace();
                    if (ch == '"' || ch == '\'')
                    {
                        uri = this.m_current.ScanLiteral(this.m_sb, ch);
                    }
                    else if (ch != '>')
                    {
                        this.m_current.Error("Expecting system identifier literal but found '{0}'", ch);
                    }
                    e = new Entity(name, pubid, uri, this.m_current.Proxy);
                }
            }
            ch = this.m_current.SkipWhitespace();
            if (ch == '-')
                ch = ParseDeclComments();
            if (ch != '>')
            {
                this.m_current.Error("Expecting end of entity declaration '>' but found '{0}'", ch);
            }
            if (pe)
                this.m_pentities.Add(e.Name, e);
            else
                this.m_entities.Add(e.Name, e);
        }

        private void ParseElementDecl()
        {
            char ch = this.m_current.SkipWhitespace();
            string[] names = ParseNameGroup(ch, true);
            ch = char.ToUpperInvariant(this.m_current.SkipWhitespace());
            bool sto = false;
            bool eto = false;
            if (ch == 'O' || ch == '-')
            {
                sto = (ch == 'O'); // start tag optional?   
                this.m_current.ReadChar();
                ch = char.ToUpperInvariant(this.m_current.SkipWhitespace());
                if (ch == 'O' || ch == '-')
                {
                    eto = (ch == 'O'); // end tag optional? 
                    ch = this.m_current.ReadChar();
                }
            }
            ch = this.m_current.SkipWhitespace();
            ContentModel cm = ParseContentModel(ch);
            ch = this.m_current.SkipWhitespace();

            string[] exclusions = null;
            string[] inclusions = null;

            if (ch == '-')
            {
                ch = this.m_current.ReadChar();
                if (ch == '(')
                {
                    exclusions = ParseNameGroup(ch, true);
                    ch = this.m_current.SkipWhitespace();
                }
                else if (ch == '-')
                {
                    ch = ParseDeclComment(false);
                }
                else
                {
                    this.m_current.Error("Invalid syntax at '{0}'", ch);
                }
            }

            if (ch == '-')
                ch = ParseDeclComments();

            if (ch == '+')
            {
                ch = this.m_current.ReadChar();
                if (ch != '(')
                {
                    this.m_current.Error("Expecting inclusions name group", ch);
                }
                inclusions = ParseNameGroup(ch, true);
                ch = this.m_current.SkipWhitespace();
            }

            if (ch == '-')
                ch = ParseDeclComments();


            if (ch != '>')
            {
                this.m_current.Error("Expecting end of ELEMENT declaration '>' but found '{0}'", ch);
            }

            foreach (string name in names)
            {
                string atom = name.ToUpperInvariant();
                this.m_elements.Add(atom, new ElementDecl(atom, sto, eto, cm, inclusions, exclusions));
            }
        }

        const string ngterm = " \r\n\t|,)";
        string[] ParseNameGroup(char ch, bool nmtokens)
        {
            ArrayList names = new ArrayList();
            if (ch == '(')
            {
                ch = this.m_current.ReadChar();
                ch = this.m_current.SkipWhitespace();
                while (ch != ')')
                {
                    // skip whitespace, scan name (which may be parameter entity reference
                    // which is then expanded to a name)                    
                    ch = this.m_current.SkipWhitespace();
                    if (ch == '%')
                    {
                        Entity e = ParseParameterEntity(SgmlDtd.ngterm);
                        PushEntity(this.m_current.ResolvedUri, e);
                        ParseNameList(names, nmtokens);
                        PopEntity();
                        ch = this.m_current.Lastchar;
                    }
                    else
                    {
                        string token = this.m_current.ScanToken(this.m_sb, SgmlDtd.ngterm, nmtokens);
                        token = token.ToUpperInvariant();
                        names.Add(token);
                    }
                    ch = this.m_current.SkipWhitespace();
                    if (ch == '|' || ch == ',') ch = this.m_current.ReadChar();
                }
                this.m_current.ReadChar(); // consume ')'
            }
            else
            {
                string name = this.m_current.ScanToken(this.m_sb, SgmlDtd.WhiteSpace, nmtokens);
                name = name.ToUpperInvariant();
                names.Add(name);
            }
            return (string[])names.ToArray(typeof(string));
        }

        void ParseNameList(ArrayList names, bool nmtokens)
        {
            char ch = this.m_current.Lastchar;
            ch = this.m_current.SkipWhitespace();
            while (ch != Entity.EOF)
            {
                string name;
                if (ch == '%')
                {
                    Entity e = ParseParameterEntity(SgmlDtd.ngterm);
                    PushEntity(this.m_current.ResolvedUri, e);
                    ParseNameList(names, nmtokens);
                    PopEntity();
                    ch = this.m_current.Lastchar;
                }
                else
                {
                    name = this.m_current.ScanToken(this.m_sb, SgmlDtd.ngterm, true);
                    name = name.ToUpperInvariant();
                    names.Add(name);
                }
                ch = this.m_current.SkipWhitespace();
                if (ch == '|')
                {
                    ch = this.m_current.ReadChar();
                    ch = this.m_current.SkipWhitespace();
                }
            }
        }

        const string dcterm = " \r\n\t>";
        private ContentModel ParseContentModel(char ch)
        {
            ContentModel cm = new ContentModel();
            if (ch == '(')
            {
                this.m_current.ReadChar();
                ParseModel(')', cm);
                ch = this.m_current.ReadChar();
                if (ch == '?' || ch == '+' || ch == '*')
                {
                    cm.AddOccurrence(ch);
                    this.m_current.ReadChar();
                }
            }
            else if (ch == '%')
            {
                Entity e = ParseParameterEntity(SgmlDtd.dcterm);
                PushEntity(this.m_current.ResolvedUri, e);
                cm = ParseContentModel(this.m_current.Lastchar);
                PopEntity(); // bugbug should be at EOF.
            }
            else
            {
                string dc = ScanName(SgmlDtd.dcterm);
                cm.SetDeclaredContent(dc);
            }
            return cm;
        }

        const string cmterm = " \r\n\t,&|()?+*";
        void ParseModel(char cmt, ContentModel cm)
        {
            // Called when part of the model is made up of the contents of a parameter entity
            int depth = cm.CurrentDepth;
            char ch = this.m_current.Lastchar;
            ch = this.m_current.SkipWhitespace();
            while (ch != cmt || cm.CurrentDepth > depth) // the entity must terminate while inside the content model.
            {
                if (ch == Entity.EOF)
                {
                    this.m_current.Error("Content Model was not closed");
                }
                if (ch == '%')
                {
                    Entity e = ParseParameterEntity(SgmlDtd.cmterm);
                    PushEntity(this.m_current.ResolvedUri, e);
                    ParseModel(Entity.EOF, cm);
                    PopEntity();
                    ch = this.m_current.SkipWhitespace();
                }
                else if (ch == '(')
                {
                    cm.PushGroup();
                    this.m_current.ReadChar();// consume '('
                    ch = this.m_current.SkipWhitespace();
                }
                else if (ch == ')')
                {
                    ch = this.m_current.ReadChar();// consume ')'
                    if (ch == '*' || ch == '+' || ch == '?')
                    {
                        cm.AddOccurrence(ch);
                        ch = this.m_current.ReadChar();
                    }
                    if (cm.PopGroup() < depth)
                    {
                        this.m_current.Error("Parameter entity cannot close a paren outside it's own scope");
                    }
                    ch = this.m_current.SkipWhitespace();
                }
                else if (ch == ',' || ch == '|' || ch == '&')
                {
                    cm.AddConnector(ch);
                    this.m_current.ReadChar(); // skip connector
                    ch = this.m_current.SkipWhitespace();
                }
                else
                {
                    string token;
                    if (ch == '#')
                    {
                        ch = this.m_current.ReadChar();
                        token = "#" + this.m_current.ScanToken(this.m_sb, SgmlDtd.cmterm, true); // since '#' is not a valid name character.
                    }
                    else
                    {
                        token = this.m_current.ScanToken(this.m_sb, SgmlDtd.cmterm, true);
                    }

                    token = token.ToUpperInvariant();
                    ch = this.m_current.Lastchar;
                    if (ch == '?' || ch == '+' || ch == '*')
                    {
                        cm.PushGroup();
                        cm.AddSymbol(token);
                        cm.AddOccurrence(ch);
                        cm.PopGroup();
                        this.m_current.ReadChar(); // skip connector
                        ch = this.m_current.SkipWhitespace();
                    }
                    else
                    {
                        cm.AddSymbol(token);
                        ch = this.m_current.SkipWhitespace();
                    }
                }
            }
        }

        void ParseAttList()
        {
            char ch = this.m_current.SkipWhitespace();
            string[] names = ParseNameGroup(ch, true);
            Dictionary<string, AttDef> attlist = new Dictionary<string, AttDef>();
            ParseAttList(attlist, '>');
            foreach (string name in names)
            {
                if (!m_elements.TryGetValue(name, out ElementDecl e))
                {
                    this.m_current.Error("ATTLIST references undefined ELEMENT {0}", name);
                }

                e.AddAttDefs(attlist);
            }
        }

        const string peterm = " \t\r\n>";
        void ParseAttList(Dictionary<string, AttDef> list, char term)
        {
            char ch = this.m_current.SkipWhitespace();
            while (ch != term)
            {
                if (ch == '%')
                {
                    Entity e = ParseParameterEntity(SgmlDtd.peterm);
                    PushEntity(this.m_current.ResolvedUri, e);
                    ParseAttList(list, Entity.EOF);
                    PopEntity();
                    ch = this.m_current.SkipWhitespace();
                }
                else if (ch == '-')
                {
                    ch = ParseDeclComments();
                }
                else
                {
                    AttDef a = ParseAttDef(ch);
                    list.Add(a.Name, a);
                }
                ch = this.m_current.SkipWhitespace();
            }
        }

        AttDef ParseAttDef(char ch)
        {
            ch = this.m_current.SkipWhitespace();
            string name = ScanName(SgmlDtd.WhiteSpace);
            name = name.ToUpperInvariant();
            AttDef attdef = new AttDef(name);

            ch = this.m_current.SkipWhitespace();
            if (ch == '-')
                ch = ParseDeclComments();

            ParseAttType(ch, attdef);

            ch = this.m_current.SkipWhitespace();
            if (ch == '-')
                ch = ParseDeclComments();

            ParseAttDefault(ch, attdef);

            ch = this.m_current.SkipWhitespace();
            if (ch == '-')
                ch = ParseDeclComments();

            return attdef;

        }

        void ParseAttType(char ch, AttDef attdef)
        {
            if (ch == '%')
            {
                Entity e = ParseParameterEntity(SgmlDtd.WhiteSpace);
                PushEntity(this.m_current.ResolvedUri, e);
                ParseAttType(this.m_current.Lastchar, attdef);
                PopEntity(); // bugbug - are we at the end of the entity?
                ch = this.m_current.Lastchar;
                return;
            }

            if (ch == '(')
            {
                //attdef.EnumValues = ParseNameGroup(ch, false);  
                //attdef.Type = AttributeType.ENUMERATION;
                attdef.SetEnumeratedType(ParseNameGroup(ch, false), AttributeType.ENUMERATION);
            }
            else
            {
                string token = ScanName(SgmlDtd.WhiteSpace);
                if (string.Equals(token, "NOTATION", StringComparison.OrdinalIgnoreCase))
                {
                    ch = this.m_current.SkipWhitespace();
                    if (ch != '(')
                    {
                        this.m_current.Error("Expecting name group '(', but found '{0}'", ch);
                    }
                    //attdef.Type = AttributeType.NOTATION;
                    //attdef.EnumValues = ParseNameGroup(ch, true);
                    attdef.SetEnumeratedType(ParseNameGroup(ch, true), AttributeType.NOTATION);
                }
                else
                {
                    attdef.SetType(token);
                }
            }
        }

        void ParseAttDefault(char ch, AttDef attdef)
        {
            if (ch == '%')
            {
                Entity e = ParseParameterEntity(WhiteSpace);
                PushEntity(this.m_current.ResolvedUri, e);
                ParseAttDefault(this.m_current.Lastchar, attdef);
                PopEntity(); // bugbug - are we at the end of the entity?
                ch = this.m_current.Lastchar;
                return;
            }

            bool hasdef = true;
            if (ch == '#')
            {
                this.m_current.ReadChar();
                string token = this.m_current.ScanToken(this.m_sb, WhiteSpace, true);
                hasdef = attdef.SetPresence(token);
                ch = this.m_current.SkipWhitespace();
            }
            if (hasdef)
            {
                if (ch == '\'' || ch == '"')
                {
                    string lit = this.m_current.ScanLiteral(this.m_sb, ch);
                    attdef.Default = lit;
                    _ = this.m_current.SkipWhitespace();
                }
                else
                {
                    string name = this.m_current.ScanToken(this.m_sb, SgmlDtd.WhiteSpace, false);
                    name = name.ToUpperInvariant();
                    attdef.Default = name; // bugbug - must be one of the enumerated names.
                    _ = this.m_current.SkipWhitespace();
                }
            }
        }
    }
}