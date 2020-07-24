using System;

namespace Carbon.Readability
{
    public sealed class TranscodeRequest
    {
        private DomSerializationParams? _domSerializationParams;

        public TranscodeRequest(string htmlContent)
        {
            if (string.IsNullOrEmpty(htmlContent))
            {
                throw new ArgumentException("Argument can't be null nor empty.", "htmlContent");
            }

            HtmlContent = htmlContent;
        }

        public TranscodeRequest(string htmlContent, string url)
        {
            if (string.IsNullOrEmpty(htmlContent))
            {
                throw new ArgumentException("Argument can't be null nor empty.", "htmlContent");
            }

            HtmlContent = htmlContent;
            Url = url;
        }

        public string HtmlContent { get; private set; }

        public string? Url { get; set; }

        public DomSerializationParams DomSerializationParams
        {
            get => _domSerializationParams ??= DomSerializationParams.CreateDefault();
            set => _domSerializationParams = value;
        }
    }
}
