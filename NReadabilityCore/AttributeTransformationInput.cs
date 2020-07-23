using System.Xml.Linq;

namespace NReadability
{
    public sealed class AttributeTransformationInput
    {
        public string AttributeValue { get; set; }

        public XElement Element { get; set; }
    }
}
