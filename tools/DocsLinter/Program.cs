using CommandLine;

namespace DocsLinter
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            return Parser.Default.ParseArguments<LinkCheckCommand.Options>(args)
                .MapResult(
                    linkCheckOptions => new LinkCheckCommand(linkCheckOptions).Run(),
                    parsingErrors => 1);
        }
    }
}
