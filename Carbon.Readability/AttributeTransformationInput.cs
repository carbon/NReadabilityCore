using System.Xml.Linq;

namespace Carbon.Readability
{
    public readonly struct AttributeTransformationInput
    {
        public AttributeTransformationInput(string attributeValue, XElement? element)
        {
            AttributeValue = attributeValue;
            Element = element;
        }

        public string AttributeValue { get; }

        public XElement? Element { get; }
    }
}
