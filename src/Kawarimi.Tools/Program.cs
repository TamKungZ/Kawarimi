using Kawarimi.Tools.Cli;

namespace Kawarimi.Tools;

internal static class Program
{
    static int Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        return args.Length == 0 ? Menu.Run() : CommandLine.Run(args);
    }
}
