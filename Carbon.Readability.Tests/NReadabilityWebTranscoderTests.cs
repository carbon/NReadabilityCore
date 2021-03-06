/*
 * NReadability
 * http://code.google.com/p/nreadability/
 * 
 * Copyright 2010 Marek St�j
 * http://immortal.pl/
 * 
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 * 
 * http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using NUnit.Framework;

namespace Carbon.Readability.Tests
{
    [TestFixture]
    public class ReadabilityWebTranscoderTests
    {
        #region Test URLs

        /* This provides the list of URLs for the different test imports */
        private readonly Dictionary<int, string[]> _Urls =
          new Dictionary<int, string[]>
            {
          {
            1,
            new[]
              {
                @"http://www.nytimes.com/2010/11/14/world/asia/14myanmar.html?hp",
                @"http://www.nytimes.com/2010/11/14/world/asia/14myanmar.html?pagewanted=2&hp"
              }
            },
          {
            2,
            new[]
              {
                @"http://www.vanityfair.com/politics/features/2010/12/unbroken-excerpt-201012",
                @"http://www.vanityfair.com/politics/features/2010/12/unbroken-excerpt-201012?currentPage=2",
                @"http://www.vanityfair.com/politics/features/2010/12/unbroken-excerpt-201012?currentPage=3"
              }
            },
          {
            3,
            new[]
              {
                @"http://www.theatlantic.com/magazine/archive/2010/12/dirty-coal-clean-future/8307",
                @"http://www.theatlantic.com/magazine/archive/2010/12/dirty-coal-clean-future/8307/2",
                @"http://www.theatlantic.com/magazine/archive/2010/12/dirty-coal-clean-future/8307/3"
              }
            },
          {
            4,
            new[]
              {
                @"http://www.slate.com/id/2275733",
                @"http://www.slate.com/id/2275733/pagenum/2"
              }
            },
          {
            5,
            new[]
              {
                @"http://entertainment.howstuffworks.com/leisure/brain-games/scrabble.htm",
                @"http://entertainment.howstuffworks.com/leisure/brain-games/scrabble1.htm",
                @"http://entertainment.howstuffworks.com/leisure/brain-games/scrabble2.htm",
                @"http://entertainment.howstuffworks.com/leisure/brain-games/scrabble3.htm",
                @"http://entertainment.howstuffworks.com/leisure/brain-games/scrabble4.htm",
              }
            },
          {
            6,
            new[]
              {
                @"http://www.slate.com/articles/technology/technology/2011/10/steve_jobs_biography_the_new_book_doesn_t_explain_what_made_the_.html",
                @"http://www.slate.com/articles/technology/technology/2011/10/steve_jobs_biography_the_new_book_doesn_t_explain_what_made_the_.2.html",
              }
            },
          {
            7,
            new[]
              {
                @"http://www.brookings.edu/opinions/2011/0523_transit_berube_puentes.aspx",
                @"http://www.brookings.edu/opinions/2011/0524_nextwave_west.aspx", // false positive for paging
              }
            },
          {
            8,
            new[]
              {
                @"http://mashable.com/2008/10/30/slow-feed-movement-rss",
                @"http://mashable.com/2008/10/30/indecision2008-live-chat", // false positive for paging
              }
            },
          {
            9,
            new[]
              {
                @"http://www.sparknotes.com/lit/mocking/section1.rhtml",
                @"http://www.sparknotes.com/lit/mocking/section2.rhtml",
              }
            },
          {
            10,
            new[]
              {
                @"http://www.gamasutra.com/view/feature/175210/the_ins_and_outs_of_native_client.php",
                @"http://www.gamasutra.com/view/feature/175210/the_ins_and_outs_of_native_client.php?page=2",
                @"http://www.gamasutra.com/view/feature/175210/the_ins_and_outs_of_native_client.php?page=3",
              }
            },
          {
            11,
            new[]
              {
                @"http://www.gamasutra.com/view/feature/177676/The_Media_Molecule_Identity.php",
                @"http://www.gamasutra.com/view/feature/177676/the_media_molecule_identity.php?page=2",
                @"http://www.gamasutra.com/view/feature/177676/the_media_molecule_identity.php?page=3",
              }
            },
            };

        #endregion

        // SampleWebOutput
        // [Test]
        public async Task NewYorker()
        {

            const string outputDir = "SampleWebOutput";


            var fetcher = new UrlFetcher();

            var nReadabilityTranscoder = new ReadabilityTranscoder();
            var nReadabilityWebTranscoder = new ReadabilityWebTranscoder(nReadabilityTranscoder, fetcher);

            var request = new WebTranscodeRequest("https://www.newyorker.com/culture/postscript/a-few-words-about-jerry-stiller");

            var result = await nReadabilityWebTranscoder.TranscodeAsync(request);

            Assert.IsTrue(result.ContentExtracted);


            string extractedContent = result.Content;

            File.WriteAllText(
              Path.Combine(outputDir, string.Format("SampleOutput_newyorker.html")),
              extractedContent,
              Encoding.UTF8);
        }

        [Test]
        public async Task Semplice1()
        {
            const string outputDir = "SampleWebOutput";

            var fetcher = new UrlFetcher();

            var nReadabilityTranscoder = new ReadabilityTranscoder();
            var nReadabilityWebTranscoder = new ReadabilityWebTranscoder(nReadabilityTranscoder, fetcher);

            var webTranscodingInput = new WebTranscodeRequest("https://vanschneider.com/the-art-of-pricing-freelance-projects");

            var result = await nReadabilityWebTranscoder.TranscodeAsync(webTranscodingInput);

            Assert.IsTrue(result.ContentExtracted);

            string extractedContent = result.Content;

            File.WriteAllText(
              Path.Combine(outputDir, string.Format("SampleOutput_a1.html")),
              extractedContent,
              Encoding.UTF8);
        }

        [Test]
        [Sequential]
        public async Task TestSampleInputs([Values(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11)] int sampleInputNumber)
        {
            const string outputDir = "SampleWebOutput";

            string sampleInputNumberStr = sampleInputNumber.ToString().PadLeft(2, '0');
            string[] urls = _Urls[sampleInputNumber];
            string initialUrl = urls[0];

            var fetcher = new FileBasedUrlFetcherStub(sampleInputNumber, urls);
            var nReadabilityTranscoder = new ReadabilityTranscoder();
            var nReadabilityWebTranscoder = new ReadabilityWebTranscoder(nReadabilityTranscoder, fetcher);

            var request = new WebTranscodeRequest(initialUrl);

            request.DomSerializationParams.PrettyPrint = true;

            var result = await nReadabilityWebTranscoder.TranscodeAsync(request);

            Assert.IsTrue(result.ContentExtracted);

            if (!Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            string content = result.Content;

          

            File.WriteAllText(
              Path.Combine(outputDir, string.Format("SampleOutput_{0}.html", sampleInputNumberStr)),
              content,
              Encoding.UTF8);

            switch (sampleInputNumber)
            {
                case 1:
                    Assert.IsTrue(content.Contains(" freedom of movement or expression would constitute a new and unacceptable denial"));
                    Assert.IsTrue(content.Contains("Those expectations were on display in the crowd outside her house on Saturday."));
                    Assert.That(Regex.Matches(content, "Myanmar Junta Frees Dissident Daw Aung San Suu Kyi").Count, Is.EqualTo(4));
                    break;

                case 2:
                    Assert.IsTrue(content.Contains("For Louie and Phil, the conversations did more than keep their minds sharp."));
                    Assert.IsTrue(content.Contains("It was absolutely dark and absolutely silent, save for the chattering of Phil�s teeth."));
                    Assert.IsTrue(content.Contains("A serial runaway and artful dodger"));
                    Assert.That(Regex.Matches(content, @"Adrift but Unbroken \| Politics").Count, Is.EqualTo(2));
                    break;

                case 3:
                    Assert.IsTrue(content.Contains("The Chinese system as a whole has great weaknesses as well as great strengths."));
                    Assert.IsTrue(content.Contains(" This emphasis on limits is what begins pointing us back to coal."));
                    Assert.IsTrue(content.Contains(". For example, the possibility of dramatic rises in ocean levels, which could affect the habitability"));
                    Assert.That(Regex.Matches(content, "Dirty Coal, Clean Future - Magazine").Count, Is.EqualTo(3)); // Makes sure the title isn't duplicated
                    break;

                case 4:  // Test duplicate content on subsequent page
                    Assert.That(Regex.Matches(content, "his may seem paradoxical, or backward").Count, Is.EqualTo(1));
                    break;

                case 5:
                    // page 1
                    Assert.IsTrue(content.Contains("The pressure's on, and as you glance back and forth between your rack and the board, you can hardly believe your eyes at the play you can make."));
                    Assert.IsTrue(content.Contains("How can you take your game to the next level? Let's start by looking at game play."));
                    // page 2
                    Assert.IsTrue(content.Contains("The object of Scrabble is to get the most points by creating words."));
                    Assert.IsTrue(content.Contains("Now that you know the parts of the game, let's take a look at how to play it."));
                    // page 3
                    Assert.IsTrue(content.Contains("To determine who goes first, put all the tiles into the bag and mix them up."));
                    Assert.IsTrue(content.Contains("The game continues until one player uses all of his tiles and there aren't any in the pouch, or if there are no more tiles and no one can make a word. Add up the total of your unplayed tiles and deduct it from your score. If you've used all of your tiles, add the total of the unplayed tiles to your score. The winner has the most points."));
                    // page 4
                    Assert.IsTrue(content.Contains("If you play often enough, you'll need to learn how to play the board in order to get the highest score"));
                    Assert.IsTrue(content.Contains("With the game's popularity, it now comes in many variations. Let's take a look at some different ways to play Scrabble."));
                    // page 5
                    Assert.IsTrue(content.Contains("Many people play Scrabble on a traditional flat board with the grid imprinted on it."));
                    Assert.IsTrue(content.Contains("With its worldwide popularity, it only makes sense that Scrabble comes in languages other than English. "));
                    break;

                case 6:
                    // page 1
                    Assert.IsTrue(content.Contains("In the aftermath of his resignation and then his death"));
                    Assert.IsTrue(content.Contains("Curb Your Enthusiasm"));
                    // page 2
                    Assert.IsTrue(content.Contains("Jobs also seemed to suspect that he"));
                    Assert.IsTrue(content.Contains("And, sadly, it may remain one forever."));
                    break;

                case 7:
                    // page 1
                    Assert.IsTrue(content.Contains("post also betrays some misconceptions regarding our report."));
                    Assert.IsTrue(content.Contains("After all, none of us can resist the occasional study"));
                    // "page" 2 (false positive)
                    Assert.IsFalse(content.Contains("In expressing this view, Clinton joins many Americans who worry about online misinformation, loss of privacy, and identity theft."));
                    break;

                case 8:
                    // page 1
                    Assert.IsTrue(content.Contains("For the last couple of days we�ve been asking people"));
                    Assert.IsTrue(content.Contains("list your favorite tools for slowing down feeds in the comments"));
                    // "page" 2 (false positive)
                    Assert.IsFalse(content.Contains("signature fake news programs"));
                    break;

                case 9:
                    // page 1
                    Assert.IsTrue(content.Contains("The story is narrated by a young girl named Jean Louise"));
                    Assert.IsTrue(content.Contains("toward adulthood."));
                    // page 2
                    Assert.IsTrue(content.Contains("September arrives, and Dill leaves Maycomb to return to"));
                    Assert.IsTrue(content.Contains("educational technique but the law."));
                    break;

                case 10:
                    // page 1
                    Assert.IsTrue(content.Contains("Curious about Native Client"));
                    Assert.IsTrue(content.Contains("also known as the GLES2 Blue Book"));
                    // page 2
                    Assert.IsTrue(content.Contains("Most games written specifically for PC"));
                    Assert.IsTrue(content.Contains("The things a good script should do"));
                    // page 3
                    Assert.IsTrue(content.Contains("The NaCl team is working hard on debugging"));
                    Assert.IsTrue(content.Contains("Unfortunately this isn't really documented"));
                    break;

                case 11:
                    // page 1
                    Assert.IsTrue(content.Contains("Sony press conference at Gamescom"));
                    Assert.IsTrue(content.Contains("The guys can actually model inside the game"));
                    // page 2
                    Assert.IsTrue(content.Contains("You actually fold"));
                    Assert.IsTrue(content.Contains("working on the skin shader right now"));
                    // page 3
                    Assert.IsTrue(content.Contains("It was the founding thing"));
                    Assert.IsTrue(content.Contains("opportunities that you just did not have on the PS3"));
                    break;

                default:
                    throw new NotSupportedException("Unknown sample input number (" + sampleInputNumber + "). Have you added another sample input? If so, then add appropriate asserts here as well.");
            }
        }

        [Test]
        public async Task
            Transcode_returns_title_if_it_can_be_extracted()
        {
            // arrange
            const string expectedTitle = "Some title �����";
            const string htmlContent = "<html><head><title>" + expectedTitle + "</title></head><body></body></html>";

            var nReadabilityTranscoder = new ReadabilityTranscoder();
            var urlFetcher = new SimpleUrlFetcherStub(htmlContent);
            var nReadabilityWebTranscoder = new ReadabilityWebTranscoder(nReadabilityTranscoder, urlFetcher);

            var request = new WebTranscodeRequest("http://dummy.com/");

            var result = await nReadabilityWebTranscoder.TranscodeAsync(request);

            Assert.NotNull(result.Title);
            Assert.AreEqual(expectedTitle, result.Title);
        }
    }
}

/*
TODO IMM HI: upnext, next in, next on
http://www.observer.com/2011/manohla-dargis-whoopis-perceived-slight-misses-point?utm_medium=partial-text&utm_campaign=media
*/
