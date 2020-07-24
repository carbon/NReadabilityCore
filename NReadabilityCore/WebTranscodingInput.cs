using System;

namespace Carbon.Readability
{
    public sealed class WebTranscodeRequest
    {
        private DomSerializationParams? _domSerializationParams;

        public WebTranscodeRequest(string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                throw new ArgumentException("Argument can't be null nor empty.", "url");
            }

            Url = url;
        }

        public string Url { get; private set; }

        public DomSerializationParams DomSerializationParams
        {
            get { return _domSerializationParams ??= DomSerializationParams.CreateDefault(); }
            set { _domSerializationParams = value; }
        }
    }
}
