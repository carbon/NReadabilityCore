using System;

namespace Carbon.Readability
{
    public static class HtmlUtils
    {
        public static ReadOnlySpan<char> RemoveScriptTags(ReadOnlySpan<char> htmlContent)
        {
            if (htmlContent == null)
            {
                throw new ArgumentNullException("htmlContent");
            }

            if (htmlContent.Length == 0)
            {
                return "";
            }

            int indexOfScriptTagStart = htmlContent.IndexOf("<script", StringComparison.OrdinalIgnoreCase);

            if (indexOfScriptTagStart == -1)
            {
                return htmlContent;
            }

            int indexOfScriptTagEnd = htmlContent.Slice(indexOfScriptTagStart).IndexOf("</script>", StringComparison.OrdinalIgnoreCase);

            if (indexOfScriptTagEnd == -1)
            {
                return htmlContent.Slice(0, indexOfScriptTagStart);
            }

            string strippedHtmlContent =
                string.Concat(
                    htmlContent.Slice(0, indexOfScriptTagStart),
                    htmlContent.Slice(indexOfScriptTagStart + indexOfScriptTagEnd + "</script>".Length)
               );

            return RemoveScriptTags(strippedHtmlContent);
        }
    }
}
