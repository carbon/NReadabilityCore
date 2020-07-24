namespace Carbon.Readability
{
    public sealed class AttributeTransformationResult
    {
        public AttributeTransformationResult(string transformedValue)
        {
            this.TransformedValue = transformedValue;
        }

        /// <summary>
        /// Result of the transformation.
        /// </summary>
        public string TransformedValue { get; }

        /// <summary>
        /// Name of the attribute that will be used to store the original value. Can be null.
        /// </summary>
        public string? OriginalValueAttributeName { get; set; }
    }
}
