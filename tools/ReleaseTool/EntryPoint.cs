using CommandLine;

namespace ReleaseTool
{
    internal static class EntryPoint
    {
        private static int Main(string[] args)
        {
            return Parser.Default.ParseArguments<PrepCommand.Options, ReleaseCommand.Options>(args)
                .MapResult(
                    (PrepCommand.Options options) => new PrepCommand(options).Run(),
                    (ReleaseCommand.Options options) => new ReleaseCommand(options).Run(),
                    errors => 1);
        }
    }
}
