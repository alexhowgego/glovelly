using System.Text.Json;

namespace Glovelly.Api.Services;

public static class McpSchema
{
    public static object Object(object properties, IReadOnlyList<string>? required = null)
    {
        var schema = new Dictionary<string, object>
        {
            ["type"] = "object",
            ["properties"] = properties,
        };

        if (required is not null)
        {
            schema["required"] = required;
        }

        return schema;
    }

    public static object Array(object items, string? description = null)
    {
        var schema = new Dictionary<string, object>
        {
            ["type"] = "array",
            ["items"] = items,
        };

        AddIfPresent(schema, "description", description);
        return schema;
    }

    public static object String(string? description = null, int? maxLength = null)
    {
        var schema = new Dictionary<string, object>
        {
            ["type"] = "string",
        };

        AddIfPresent(schema, "description", description);
        AddIfPresent(schema, "maxLength", maxLength);
        return schema;
    }

    public static object Uuid(string? description = null)
    {
        var schema = new Dictionary<string, object>
        {
            ["type"] = "string",
            ["format"] = "uuid",
        };

        AddIfPresent(schema, "description", description);
        return schema;
    }

    public static object Date(string? description = "ISO 8601 calendar date in YYYY-MM-DD format.")
    {
        var schema = new Dictionary<string, object>
        {
            ["type"] = "string",
            ["format"] = "date",
        };

        AddIfPresent(schema, "description", description);
        return schema;
    }

    public static object Time(string? description = "Local time in HH:mm format.")
    {
        var schema = new Dictionary<string, object>
        {
            ["type"] = "string",
            ["format"] = "time",
        };

        AddIfPresent(schema, "description", description);
        return schema;
    }

    public static object DateTime(string? description = "ISO 8601 UTC timestamp.")
    {
        var schema = new Dictionary<string, object>
        {
            ["type"] = "string",
            ["format"] = "date-time",
        };

        AddIfPresent(schema, "description", description);
        return schema;
    }

    public static object Enum<TEnum>(string? description = null)
        where TEnum : struct, Enum
    {
        return Enum(System.Enum.GetNames<TEnum>().Select(JsonNamingPolicy.CamelCase.ConvertName).ToArray(), description);
    }

    public static object Enum(IReadOnlyList<string> values, string? description = null)
    {
        var schema = new Dictionary<string, object>
        {
            ["type"] = "string",
            ["enum"] = values,
        };

        AddIfPresent(schema, "description", description);
        return schema;
    }

    public static object Money(string? description = null, decimal? minimum = null)
    {
        var schema = new Dictionary<string, object>
        {
            ["type"] = "number",
        };

        AddIfPresent(schema, "description", description);
        AddIfPresent(schema, "minimum", minimum);
        return schema;
    }

    private static void AddIfPresent(Dictionary<string, object> schema, string key, object? value)
    {
        if (value is not null)
        {
            schema[key] = value;
        }
    }
}
