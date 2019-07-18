using Markdig.Syntax.Inlines;
using NUnit.Framework;

namespace DocsLinter.Tests
{
    [TestFixture]
    internal class RemoteLinkCheckingTests
    {
        private static RemoteLink GetUrlForStatusCode(int statusCode)
        {
            var url = $"http://httpstat.us/{statusCode}";
            return new RemoteLink(new LinkInline(url, ""));
        }

        [OneTimeSetUp]
        public void IgnoreSslErrors()
        {
            System.Net.ServicePointManager.ServerCertificateValidationCallback +=
                (sender, cert, chain, sslPolicyErrors) => true;
        }

        [Test]
        public void CheckRemoteLink_returns_true_for_200_status_code()
        {
            var linkChecker = new LinkCheckCommand(new LinkCheckCommand.Options());
            var result = linkChecker.CheckRemoteLink(string.Empty, GetUrlForStatusCode(200));
            Assert.IsTrue(result);
        }

        [Test]
        public void CheckRemoteLink_returns_false_for_404_status_code()
        {
            var linkChecker = new LinkCheckCommand(new LinkCheckCommand.Options());
            var result = linkChecker.CheckRemoteLink(string.Empty, GetUrlForStatusCode(404));
            Assert.IsFalse(result);
        }

        [Test]
        public void CheckRemoteLink_returns_true_for_arbitrary_status_code()
        {
            var linkChecker = new LinkCheckCommand(new LinkCheckCommand.Options());
            // Permanent redirect
            var result = linkChecker.CheckRemoteLink(string.Empty, GetUrlForStatusCode(301));
            Assert.IsTrue(result);
        }
    }
}
