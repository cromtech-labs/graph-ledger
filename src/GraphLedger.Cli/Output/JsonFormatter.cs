using System.Text.Json;

namespace GraphLedger.Cli.Output;

public static class JsonFormatter
{
    private static readonly JsonSerializerOptions DefaultOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static string Format<T>(T obj)
    {
        return JsonSerializer.Serialize(obj, DefaultOptions);
    }

    public static void Write<T>(T obj)
    {
        Console.WriteLine(Format(obj));
    }

    public static string FormatPretty(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return JsonSerializer.Serialize(doc, DefaultOptions);
        }
        catch
        {
            return json;
        }
    }
}
