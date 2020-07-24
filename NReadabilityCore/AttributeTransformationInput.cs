using System.Xml.Linq;

namespace Carbon.Readability
{
    public sealed class AttributeTransformationInput
    {
        public string AttributeValue { get; set; }

        public XElement Element { get; set; }
    }
}
