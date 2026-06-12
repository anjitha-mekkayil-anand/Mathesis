namespace Mathesis.Agent.MultiAgent;

/// <summary>
/// Colored console narration — the pipeline's hops should read like a story.
/// Teal family = agents reasoning · yellow = human gates and warnings ·
/// green / yellow / red = Ready / Borderline / NotReady.
/// </summary>
public static class Narrate
{
    public static void Line(string tag, string message, ConsoleColor tagColor = ConsoleColor.Cyan, ConsoleColor? messageColor = null)
    {
        Console.ForegroundColor = tagColor;
        Console.Write($"{tag,-15} ");
        if (messageColor is { } color)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(message);
            Console.ResetColor();
        }
        else
        {
            Console.ResetColor();
            Console.WriteLine(message);
        }
    }

    public static ConsoleColor BandColor(string band) => band switch
    {
        "Ready" => ConsoleColor.Green,
        "Borderline" => ConsoleColor.Yellow,
        _ => ConsoleColor.Red
    };
}
