using Markdig;
using Markdig.Syntax.Inlines;
using NUnit.Framework;

namespace DocsLinter.Tests
{
    [TestFixture]
    public class SimplifiedMarkdownDocTests
    {
        private static readonly string Title = "Title";

        private SimplifiedMarkdownDoc GetSimplifiedMarkdown(RemoteLink remoteLink)
        {
            var markdownUrl = $"[{Title}]({remoteLink.Url})";
            var markdownDoc = Markdown.Parse(markdownUrl);
            return new SimplifiedMarkdownDoc(markdownDoc, "Testing/File/Path");
        }

        private RemoteLink CreateRemoteLink(string url)
        {
            return new RemoteLink(new LinkInline(url, Title));
        }

        [Test]
        public void Link_starting_with_http_is_valid()
        {
            var url = CreateRemoteLink("http://www.google.com");
            var markdown = GetSimplifiedMarkdown(url);

            Assert.IsTrue(markdown.Links.Count == 1);
            Assert.IsTrue(markdown.Links.Contains(url));
        }

        [Test]
        public void Link_starting_with_https_is_valid()
        {
            var url = CreateRemoteLink("https://www.google.com");
            var markdown = GetSimplifiedMarkdown(url);

            Assert.IsTrue(markdown.Links.Count == 1);
            Assert.IsTrue(markdown.Links.Contains(url));
        }

        [Test]
        public void Warn_if_link_starts_with_ftp()
        {
            LinkCheckCommand.warnings = null;

            var url = CreateRemoteLink("ftp://username:hunter2@server/path");
            var markdown = GetSimplifiedMarkdown(url);

            Assert.IsTrue(LinkCheckCommand.warnings != null);
        }

        [Test]
        public void Warn_if_link_starts_with_file()
        {
            LinkCheckCommand.warnings = null;

            var url = CreateRemoteLink("file://localhost/path/to/file");
            var markdown = GetSimplifiedMarkdown(url);

            Assert.IsTrue(LinkCheckCommand.warnings != null);
        }

        [Test]
        public void Ignore_link_starting_with_site_name()
        {
            var url = CreateRemoteLink("google.com");
            var markdown = GetSimplifiedMarkdown(url);

            Assert.IsTrue(markdown.Links.Count == 0);
            Assert.IsFalse(markdown.Links.Contains(url));
        }

        [Test]
        public void Ignore_link_starting_with_www()
        {
            var url = CreateRemoteLink("www.google.com");
            var markdown = GetSimplifiedMarkdown(url);

            Assert.IsTrue(markdown.Links.Count == 0);
            Assert.IsFalse(markdown.Links.Contains(url));
        }

        [Test]
        public void Ignore_relative_link_to_file()
        {
            var url = CreateRemoteLink("../README.md");
            var markdown = GetSimplifiedMarkdown(url);

            Assert.IsTrue(markdown.Links.Count == 0);
            Assert.IsFalse(markdown.Links.Contains(url));
        }

        [Test]
        public void Error_if_relative_link_to_file()
        {
            LinkCheckCommand.errors = null;

            var url = CreateRemoteLink("../README.md");
            var markdown = GetSimplifiedMarkdown(url);

            Assert.IsTrue(LinkCheckCommand.errors != null);
        }

        [Test]
        public void Ignore_link_to_header_in_same_file()
        {
            var url = CreateRemoteLink("#other-heading-in-doc");
            var markdown = GetSimplifiedMarkdown(url);

            Assert.IsTrue(markdown.Links.Count == 0);
            Assert.IsFalse(markdown.Links.Contains(url));
        }

        [Test]
        public void Ignore_link_to_header_in_different_file()
        {
            var url = CreateRemoteLink("../README.md#other-heading-in-doc");
            var markdown = GetSimplifiedMarkdown(url);

            Assert.IsTrue(markdown.Links.Count == 0);
            Assert.IsFalse(markdown.Links.Contains(url));
        }

        [Test]
        public void Error_if_link_to_header_in_different_file()
        {
            LinkCheckCommand.errors = null;

            var url = CreateRemoteLink("../README.md#other-heading-in-doc");
            var markdown = GetSimplifiedMarkdown(url);

            Assert.IsTrue(LinkCheckCommand.errors != null);
        }

        [Test]
        public void Ignore_link_to_console()
        {
            var url = CreateRemoteLink("https://console.improbable.io/");
            var markdown = GetSimplifiedMarkdown(url);

            Assert.IsTrue(markdown.Links.Count == 0);
            Assert.IsFalse(markdown.Links.Contains(url));
        }
    }
}
