using NUnit.Framework;

namespace Carbon.Readability.Tests
{
    [TestFixture]
    public class SgmlDomSerializerTests
    {
        private SgmlDomSerializer _sgmlDomSerializer;

        [SetUp]
        public void SetUp()
        {
            _sgmlDomSerializer = new SgmlDomSerializer();
        }

        #region Tests

        [Test]
        public void Serializer_removes_viewport_meta_element_if_DontIncludeMobileSpecificElements_is_false()
        {
            // arrange
            const string htmlContent = "<html><head><meta name=\"viewport\" content=\"width=1100\" /></head><body></body></html>";

            var xDocument = SgmlDomBuilder.BuildDocument(htmlContent);

          
            // act
            string serializedHtmlContent =
              _sgmlDomSerializer.SerializeDocument(xDocument, new DomSerializationParams { PrettyPrint = true });

            throw new System.Exception(serializedHtmlContent);

            // assert
            AssertViewportMetaElementPresence(serializedHtmlContent, false);
        }

        /*
        [Test]
        public void Serializer_removes_viewport_meta_element_if_DontIncludeMobileSpecificElements_is_true()
        {
            // arrange
            const string htmlContent = "<html><head><meta name=\"viewport\" content=\"width=1100\" /></head><body></body></html>";

            var xDocument = SgmlDomBuilder.BuildDocument(htmlContent);

            var domSerializationParams =
              new DomSerializationParams
              {
                  DontIncludeMobileSpecificMetaElements = true,
              };

            // act
            string serializedHtmlContent =
              _sgmlDomSerializer.SerializeDocument(xDocument, domSerializationParams);

            // assert
            AssertViewportMetaElementPresence(serializedHtmlContent, false);
        }
        */

        /*
        [Test]
        public void Serializer_adds_content_type_meta_element_if_DontIncludeContentTypeMetaElement_is_false()
        {
            // arrange
            const string htmlContent = "<html><head></head><body></body></html>";

            var xDocument = SgmlDomBuilder.BuildDocument(htmlContent);

            var domSerializationParams =
              new DomSerializationParams
              {
                  DontIncludeContentTypeMetaElement = false,
              };

            // act
            string serializedHtmlContent =
              _sgmlDomSerializer.SerializeDocument(xDocument, domSerializationParams);

            // assert
            AssertContentTypeMetaElementPresence(serializedHtmlContent, false);
        }
        */

        [Test]
        public void Serializer_removes_existing_generator_meta_element()
        {
            // arrange
            const string htmlContent = "<html><head><meta name=\"generator\" value=\"WordPress\"</head><body></body></html>";

            var xDocument = SgmlDomBuilder.BuildDocument(htmlContent);

            // act
            string serializedHtmlContent = _sgmlDomSerializer.SerializeDocument(xDocument);

            // assert
            MyAssert.AssertSubstringCount(1, serializedHtmlContent, "<meta name=\"Generator\"");
        }

        [Test]
        public void Serializer_removes_existing_content_type_meta_element()
        {
            // arrange
            const string htmlContent = "<html><head><meta http-equiv=\"Content-Type\" value=\"UTF-8\"</head><body></body></html>";

            var xDocument = SgmlDomBuilder.BuildDocument(htmlContent);

            // act
            string serializedHtmlContent = _sgmlDomSerializer.SerializeDocument(xDocument);

            // assert
            MyAssert.AssertSubstringCount(0, serializedHtmlContent, "<meta http-equiv=\"Content-Type\"");
        }

        #endregion

        #region Private helper methods

        private static void AssertMetaElementPresence(string htmlContent, string metaElementAttributeName, string metaElementName, bool presenceIsExpected)
        {
            bool containsCondition =
              htmlContent.ToLower()
                .Contains(
                  string.Format("<meta {0}=\"{1}\"",
                    metaElementAttributeName.ToLower(),
                    metaElementName.ToLower()));

            if (presenceIsExpected)
            {
                Assert.IsTrue(containsCondition);
            }
            else
            {
                Assert.IsFalse(containsCondition);
            }
        }

        private static void AssertViewportMetaElementPresence(string htmlContent, bool presenceIsExpected)
        {
            AssertMetaElementPresence(htmlContent, "name", "viewport", presenceIsExpected);
        }

        private static void AssertHandheldFriendlyMetaElementPresence(string htmlContent, bool presenceIsExpected)
        {
            AssertMetaElementPresence(htmlContent, "name", "HandheldFriendly", presenceIsExpected);
        }

        private static void AssertGeneratorMetaElementPresence(string htmlContent, bool presenceIsExpected)
        {
            AssertMetaElementPresence(htmlContent, "name", "generator", presenceIsExpected);
        }

        private static void AssertContentTypeMetaElementPresence(string htmlContent, bool presenceIsExpected)
        {
            AssertMetaElementPresence(htmlContent, "http-equiv", "Content-Type", presenceIsExpected);
        }

        #endregion
    }
}
