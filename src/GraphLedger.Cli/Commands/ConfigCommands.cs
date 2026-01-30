using System.CommandLine;
using System.Text.Json;
using GraphLedger.Cli.Output;

namespace GraphLedger.Cli.Commands;

public static class ConfigCommands
{
    private static readonly string ConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".graphledger",
        "config.json"
    );

    public static Command CreateConfigCommand()
    {
        var configCommand = new Command("config", "Manage GraphLedger configuration");

        configCommand.AddCommand(CreateSetCommand());
        configCommand.AddCommand(CreateGetCommand());

        return configCommand;
    }

    private static Command CreateSetCommand()
    {
        var keyArgument = new Argument<string>("key", "Configuration key (e.g., Azure:TenantId)");
        var valueArgument = new Argument<string>("value", "Configuration value");

        var setCommand = new Command("set", "Set a configuration value")
        {
            keyArgument,
            valueArgument
        };

        setCommand.SetHandler((key, value) =>
        {
            try
            {
                var config = LoadConfig();
                SetNestedValue(config, key, value);
                SaveConfig(config);
                TableFormatter.WriteSuccess($"Configuration '{key}' set successfully.");
            }
            catch (Exception ex)
            {
                TableFormatter.WriteError($"Failed to set configuration: {ex.Message}");
            }
        }, keyArgument, valueArgument);

        return setCommand;
    }

    private static Command CreateGetCommand()
    {
        var keyArgument = new Argument<string?>("key", () => null, "Configuration key to retrieve (optional)");

        var getCommand = new Command("get", "Get configuration value(s)")
        {
            keyArgument
        };

        getCommand.SetHandler((key) =>
        {
            try
            {
                var config = LoadConfig();

                if (string.IsNullOrEmpty(key))
                {
                    Console.WriteLine(JsonSerializer.Serialize(config, new JsonSerializerOptions
                    {
                        WriteIndented = true
                    }));
                }
                else
                {
                    var value = GetNestedValue(config, key);
                    if (value != null)
                    {
                        if (value is JsonElement element)
                        {
                            Console.WriteLine(element.ToString());
                        }
                        else
                        {
                            Console.WriteLine(value);
                        }
                    }
                    else
                    {
                        TableFormatter.WriteWarning($"Configuration key '{key}' not found.");
                    }
                }
            }
            catch (Exception ex)
            {
                TableFormatter.WriteError($"Failed to get configuration: {ex.Message}");
            }
        }, keyArgument);

        return getCommand;
    }

    private static Dictionary<string, object> LoadConfig()
    {
        if (!File.Exists(ConfigPath))
        {
            return new Dictionary<string, object>();
        }

        var json = File.ReadAllText(ConfigPath);
        return JsonSerializer.Deserialize<Dictionary<string, object>>(json)
            ?? new Dictionary<string, object>();
    }

    private static void SaveConfig(Dictionary<string, object> config)
    {
        var directory = Path.GetDirectoryName(ConfigPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        File.WriteAllText(ConfigPath, json);
    }

    private static void SetNestedValue(Dictionary<string, object> config, string key, string value)
    {
        var parts = key.Split(':');
        var current = config;

        for (int i = 0; i < parts.Length - 1; i++)
        {
            var part = parts[i];
            if (!current.ContainsKey(part))
            {
                current[part] = new Dictionary<string, object>();
            }

            if (current[part] is JsonElement element)
            {
                var nested = JsonSerializer.Deserialize<Dictionary<string, object>>(element.GetRawText())
                    ?? new Dictionary<string, object>();
                current[part] = nested;
                current = nested;
            }
            else if (current[part] is Dictionary<string, object> nested)
            {
                current = nested;
            }
            else
            {
                var newNested = new Dictionary<string, object>();
                current[part] = newNested;
                current = newNested;
            }
        }

        current[parts[^1]] = value;
    }

    private static object? GetNestedValue(Dictionary<string, object> config, string key)
    {
        var parts = key.Split(':');
        object current = config;

        foreach (var part in parts)
        {
            if (current is Dictionary<string, object> dict)
            {
                if (!dict.TryGetValue(part, out var next))
                {
                    return null;
                }
                current = next;
            }
            else if (current is JsonElement element)
            {
                if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(part, out var prop))
                {
                    current = prop;
                }
                else
                {
                    return null;
                }
            }
            else
            {
                return null;
            }
        }

        return current;
    }
}
