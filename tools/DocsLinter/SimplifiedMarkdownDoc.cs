using System;
using System.Collections.Generic;
using System.Linq;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace DocsLinter
{
    /// <summary>
    ///     Simple data class that contains information related to a Markdown doc that we require for linting.
    /// </summary>
    public class SimplifiedMarkdownDoc
    {
        public readonly List<RemoteLink> Links = new List<RemoteLink>();

        private readonly string FilePath;

        public SimplifiedMarkdownDoc()
        {
        }

        /// <summary>
        ///     Constructor for automatically parsing a MarkdownDocument object from Markdig.
        /// </summary>
        /// <param name="markdownDoc"></param>
        /// <param name="filePath"></param>
        public SimplifiedMarkdownDoc(MarkdownDocument markdownDoc, string filePath)
        {
            FilePath = filePath;

            Links.AddRange(markdownDoc.Descendants()
                .OfType<LinkInline>()
                .Select(ParseLink)
                .Where(link => !string.IsNullOrEmpty(link.Url)));
        }

        private RemoteLink NullLink = new RemoteLink(new LinkInline(null, null));

        public RemoteLink ParseLink(LinkInline inlineLink)
        {
            var url = inlineLink.Url;

            if (!Uri.IsWellFormedUriString(url, UriKind.Absolute))
            {
                if (IsLinkToMarkdownFile(url))
                {
                    LinkCheckCommand.LogInvalidLink(FilePath, new RemoteLink(inlineLink),
                        "You must not link directly to a Markdown file.");
                }

                return NullLink;
            }

            var uriScheme = new Uri(url).Scheme;
            if (!uriScheme.Equals("http") && !uriScheme.Equals("https"))
            {
                LinkCheckCommand.LogLinkWarning(FilePath, new RemoteLink(inlineLink),
                    "is not a http or https link.");

                return NullLink;
            }

            // exclude console.improbable.io because agent is not logged in
            if (url.Contains("console.improbable.io"))
            {
                return NullLink;
            }

            return new RemoteLink(inlineLink);
        }

        private bool IsLinkToMarkdownFile(string uri)
        {
            var link = uri;
            var hashIndex = link.IndexOf("#", StringComparison.Ordinal);
            if (hashIndex != -1)
            {
                link = link.Substring(0, hashIndex);
            }

            return link.EndsWith(".md");
        }
    }

    /// <summary>
    ///     A struct that represents a remote link. I.e. - "https://www.google.com".
    /// </summary>
    public readonly struct RemoteLink
    {
        public readonly string Url;

        /// <summary>
        ///     Constructor for RemoteLink that parses the Markdig link object.
        /// </summary>
        /// <param name="link">The Markdig link object.</param>
        public RemoteLink(LinkInline link)
        {
            Url = link.Url;
        }

        public override string ToString()
        {
            return Url;
        }
    }
}
