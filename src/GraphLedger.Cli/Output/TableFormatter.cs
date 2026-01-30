using System.Text;

namespace GraphLedger.Cli.Output;

public static class TableFormatter
{
    public static string FormatTable<T>(IEnumerable<T> items, params (string Header, Func<T, string> ValueSelector)[] columns)
    {
        var itemList = items.ToList();
        if (itemList.Count == 0)
        {
            return "No data to display.";
        }

        var columnWidths = columns.Select((col, idx) =>
            Math.Max(
                col.Header.Length,
                itemList.Max(item => col.ValueSelector(item)?.Length ?? 0)
            )
        ).ToArray();

        var sb = new StringBuilder();

        // Header
        sb.AppendLine(FormatRow(columns.Select(c => c.Header).ToArray(), columnWidths));
        sb.AppendLine(new string('-', columnWidths.Sum() + (columnWidths.Length - 1) * 3 + 4));

        // Rows
        foreach (var item in itemList)
        {
            var values = columns.Select(c => c.ValueSelector(item) ?? "").ToArray();
            sb.AppendLine(FormatRow(values, columnWidths));
        }

        return sb.ToString();
    }

    private static string FormatRow(string[] values, int[] widths)
    {
        var cells = values.Select((v, i) => v.PadRight(widths[i]));
        return "| " + string.Join(" | ", cells) + " |";
    }

    public static void WriteTable<T>(IEnumerable<T> items, params (string Header, Func<T, string> ValueSelector)[] columns)
    {
        Console.WriteLine(FormatTable(items, columns));
    }

    public static void WriteSuccess(string message)
    {
        var originalColor = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine(message);
        Console.ForegroundColor = originalColor;
    }

    public static void WriteError(string message)
    {
        var originalColor = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Error.WriteLine(message);
        Console.ForegroundColor = originalColor;
    }

    public static void WriteWarning(string message)
    {
        var originalColor = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine(message);
        Console.ForegroundColor = originalColor;
    }

    public static void WriteInfo(string message)
    {
        var originalColor = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine(message);
        Console.ForegroundColor = originalColor;
    }
}
