namespace Carbon.Readability
{
    public sealed class TranscodeResult
    {
        public TranscodeResult(bool contentExtracted, bool titleExtracted)
        {
            ContentExtracted = contentExtracted;
            TitleExtracted = titleExtracted;
        }

        public bool ContentExtracted { get; private set; }

        public bool TitleExtracted { get; private set; }

        public string? Content { get; set; }

        public string? Title { get; set; }

        public string? NextPageUrl { get; set; }
    }
}
