using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace GraphLedger.Core.Services;

/// <summary>
/// Service for computing configuration hashes and extracting resource identifiers.
/// </summary>
public class ConfigurationHashService : IConfigurationHashService
{
    /// <summary>
    /// Computes SHA256 hash of normalized JSON configuration.
    /// </summary>
    public string ComputeHash(string configurationJson)
    {
        if (string.IsNullOrWhiteSpace(configurationJson))
            return ComputeSha256("");

        try
        {
            // Parse and normalize the JSON
            var normalized = NormalizeJson(configurationJson);
            return ComputeSha256(normalized);
        }
        catch (JsonException)
        {
            // If JSON is invalid, hash the raw string
            return ComputeSha256(configurationJson);
        }
    }

    /// <summary>
    /// Extracts the external ID from configuration JSON.
    /// </summary>
    public string? ExtractExternalId(string configurationJson)
    {
        if (string.IsNullOrWhiteSpace(configurationJson))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(configurationJson);
            var root = doc.RootElement;

            // Try "id" property first (most common)
            if (root.TryGetProperty("id", out var idProp))
            {
                return idProp.GetString();
            }

            // Try "Id" (different casing)
            if (root.TryGetProperty("Id", out var idProp2))
            {
                return idProp2.GetString();
            }

            // Try "@odata.id" for OData responses
            if (root.TryGetProperty("@odata.id", out var odataId))
            {
                // Extract ID from URL like "/policies/conditionalAccessPolicies('guid')"
                var url = odataId.GetString();
                if (!string.IsNullOrEmpty(url))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(url, @"'([^']+)'");
                    if (match.Success)
                        return match.Groups[1].Value;
                }
            }

            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Normalizes JSON by sorting keys and removing whitespace.
    /// This ensures equivalent JSON objects produce the same hash.
    /// </summary>
    private static string NormalizeJson(string json)
    {
        var node = JsonNode.Parse(json);
        if (node == null)
            return "";

        var normalized = NormalizeNode(node);
        return normalized?.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = false
        }) ?? "";
    }

    /// <summary>
    /// Recursively normalizes a JSON node.
    /// </summary>
    private static JsonNode? NormalizeNode(JsonNode? node)
    {
        if (node == null)
            return null;

        switch (node)
        {
            case JsonObject obj:
                // Create new object with sorted keys
                var sortedObj = new JsonObject();
                foreach (var kvp in obj.OrderBy(x => x.Key, StringComparer.Ordinal))
                {
                    sortedObj[kvp.Key] = NormalizeNode(kvp.Value?.DeepClone());
                }
                return sortedObj;

            case JsonArray arr:
                // Normalize each element
                var sortedArr = new JsonArray();
                foreach (var item in arr)
                {
                    sortedArr.Add(NormalizeNode(item?.DeepClone()));
                }
                return sortedArr;

            default:
                // Value node - return as-is
                return node.DeepClone();
        }
    }

    /// <summary>
    /// Computes SHA256 hash of a string and returns as hex.
    /// </summary>
    private static string ComputeSha256(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
