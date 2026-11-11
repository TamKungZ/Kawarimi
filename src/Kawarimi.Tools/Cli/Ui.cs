namespace Kawarimi.Tools.Cli;

internal static class Ui
{
    public static void Title(string text) => Write(ConsoleColor.Cyan, text);
    public static void Ok(string text) => Write(ConsoleColor.Green, text);
    public static void Warn(string text) => Write(ConsoleColor.Yellow, "  WARNING: " + text);
    public static void Error(string text) => Write(ConsoleColor.Red, text);
    public static void Info(string text) => Console.WriteLine(text);

    public static string Ask(string question, string? fallback = null)
    {
        Console.ForegroundColor = ConsoleColor.White;
        Console.Write(fallback == null ? $"{question}: " : $"{question} [{fallback}]: ");
        Console.ResetColor();
        var answer = Console.ReadLine()?.Trim() ?? "";
        return answer.Length == 0 && fallback != null ? fallback : answer;
    }

    public static int Choose(string question, IReadOnlyList<string> options)
    {
        for (int i = 0; i < options.Count; i++) Console.WriteLine($"  {i + 1,3}. {options[i]}");
        while (true)
        {
            var a = Ask(question);
            if (a.Length == 0) return -1;
            if (int.TryParse(a, out int n) && n >= 1 && n <= options.Count) return n - 1;
            Error($"  type a number from 1 to {options.Count}, or press Enter to cancel");
        }
    }

    static void Write(ConsoleColor color, string text)
    {
        Console.ForegroundColor = color;
        Console.WriteLine(text);
        Console.ResetColor();
    }
}
