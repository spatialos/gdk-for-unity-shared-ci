using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using CommandLine;
using Markdig;

namespace DocsLinter
{
    internal class LinkCheckCommand
    {
        [Verb("check", HelpText = "Check remote links on a GDK docs branch.")]
        public class Options
        {
            [Value(1, MetaName = "warnings-file-path", HelpText = "File path for warnings output.")]
            public string WarningsFilePath { get; set; }

            [Value(2, MetaName = "errors-file-path", HelpText = "File path for errors output.")]
            public string ErrorsFilePath { get; set; }
        }

        private readonly Options options;

        internal static StringBuilder warnings;
        internal static StringBuilder errors;

        private readonly string warningsHeading = "Warnings found:\n";
        private readonly string errorsHeading = "Errors found:\n";

        public LinkCheckCommand(Options options)
        {
            this.options = options;
        }

        public int Run()
        {
            try
            {
                var markdownFiles = GetMarkdownFiles();
                var areLinksValid = true;
                foreach (var (filePath, fileContents) in markdownFiles)
                {
                    Console.WriteLine($"Checking links for: {filePath}");
                    areLinksValid &= CheckMarkdownFile(filePath, fileContents);
                }

                PrintWarningsAndErrors();

                return areLinksValid ? 0 : 1;
            }
            catch (Exception ex)
            {
                PrintWarningsAndErrors();
                LogException(ex);
                return 1;
            }
        }

        private void PrintWarningsAndErrors()
        {
            Console.WriteLine();

            if (warnings != null)
            {
                Console.WriteLine(warningsHeading);
                Console.WriteLine(warnings);
                File.WriteAllText(options.WarningsFilePath, warnings.ToString());
            }

            if (errors != null)
            {
                Console.WriteLine(errorsHeading);
                Console.WriteLine(errors);
                File.WriteAllText(options.ErrorsFilePath, errors.ToString());
            }
        }

        /// <summary>
        ///     A helper function the collects all Markdown files from a list of file paths.
        /// </summary>
        /// <returns>A dictionary mapping a Markdown file path to the object representing that Markdown file.</returns>
        private Dictionary<string, SimplifiedMarkdownDoc> GetMarkdownFiles()
        {
            var allMarkdownFiles = Directory.GetFiles("docs", "*.md", SearchOption.AllDirectories);
            var markdownFiles = new Dictionary<string, SimplifiedMarkdownDoc>();

            foreach (var filePath in allMarkdownFiles)
            {
                var markdownDoc = Markdown.Parse(File.ReadAllText(filePath));
                var fullFilePath = Path.GetFullPath(filePath);
                markdownFiles.Add(fullFilePath, new SimplifiedMarkdownDoc(markdownDoc, fullFilePath));
            }

            return markdownFiles;
        }

        /// <summary>
        ///     A helper method that checks all the links in a markdown file and returns a success/fail.
        ///     Side effects: Prints to the console.
        /// </summary>
        /// <param name="markdownFilePath">The fully qualified path of the Markdown file to check</param>
        /// <param name="markdownFileContents">An object representing the Markdown file to check.</param>
        /// <returns>A bool indicating the success of the check.</returns>
        private bool CheckMarkdownFile(string markdownFilePath, SimplifiedMarkdownDoc markdownFileContents)
        {
            return markdownFileContents.Links.All(link => CheckRemoteLink(markdownFilePath, link));
        }

        /// <summary>
        ///     A helper function that checks the validity of a single remote link.
        ///     Side effects: Prints to the console.
        /// </summary>
        /// <param name="markdownFilePath">The fully qualified path of the Markdown file to check</param>
        /// <param name="remoteLink">The object representing the remote link to check.</param>
        /// <returns>A bool indicating success/failure</returns>
        internal bool CheckRemoteLink(string markdownFilePath, RemoteLink remoteLink)
        {
            // Necessary to be in scope in finally block.
            HttpWebResponse response = null;

            try
            {
                var strippedUrl = remoteLink.Url;

                // anchors break the link check, need to remove them from the link before creating the web request.
                if (strippedUrl.Contains("#"))
                {
                    strippedUrl = remoteLink.Url.Substring(
                        0,
                        strippedUrl.IndexOf("#", StringComparison.Ordinal));
                }

                var request = WebRequest.CreateHttp(strippedUrl);
                request.Method = WebRequestMethods.Http.Get;
                request.AllowAutoRedirect = true;
                response = request.GetResponse() as HttpWebResponse;

                // Check for non-200 error codes.
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    LogLinkWarning(markdownFilePath, remoteLink,
                        $"returned a status code of: {(int) response.StatusCode}");
                }

                return true;
            }
            catch (WebException ex)
            {
                // There was an error code. Check if it was a 404.
                // Any other 4xx errors are considered "okay" for now.
                if (ex.Status == WebExceptionStatus.ProtocolError)
                {
                    var statusCode = ((HttpWebResponse) ex.Response).StatusCode;
                    if (statusCode == HttpStatusCode.NotFound)
                    {
                        LogInvalidLink(markdownFilePath, remoteLink);
                        return false;
                    }

                    LogLinkWarning(markdownFilePath, remoteLink,
                        $"returned a status code of: {(int) statusCode}");
                    return true;
                }

                LogInvalidLink(markdownFilePath, remoteLink,
                    "An exception occured when trying to access this remote link.");
                LogException(ex);
                return false;
            }
            catch (Exception ex)
            {
                LogInvalidLink(markdownFilePath, remoteLink,
                    "An exception occured when trying to access this remote link.");
                LogException(ex);
                return false;
            }
            finally
            {
                response?.Close();
            }
        }

        /// <summary>
        ///     A helper function to print a error when an invalid link is found.
        /// </summary>
        /// <param name="markdownFilePath">The path of the markdown file the error was found in.</param>
        /// <param name="link">The link that is invalid.</param>
        /// <param name="message">Optional. The warning message to print.</param>
        internal static void LogInvalidLink(string markdownFilePath, RemoteLink link, string message = "")
        {
            var errorMessage = $"Error in {markdownFilePath}. The link: {link} is invalid. {message}";

            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(errorMessage);
            Console.ResetColor();

            if (errors == null)
            {
                errors = new StringBuilder();
            }

            errors.AppendLine(errorMessage);
        }

        /// <summary>
        ///     A helper function to print a warning about a specific link.
        /// </summary>
        /// <param name="markdownFilePath">The path of the markdown file the error was found in.</param>
        /// <param name="link">The link that the warning is about.</param>
        /// <param name="message">The warning message to print.</param>
        internal static void LogLinkWarning(string markdownFilePath, RemoteLink link, string message)
        {
            var warningMessage = $"Warning in {markdownFilePath}. The link {link} {message}";

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(warningMessage);
            Console.ResetColor();

            if (warnings == null)
            {
                warnings = new StringBuilder();
            }

            warnings.AppendLine(warningMessage);
        }

        /// <summary>
        ///     A helper function to log exceptions when they are caught.
        /// </summary>
        /// <param name="ex">The exception that was caught</param>
        internal static void LogException(Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine(ex.ToString());
            if (ex.InnerException != null)
            {
                Console.Error.WriteLine(ex.InnerException.ToString());
            }

            Console.ResetColor();
        }
    }
}
