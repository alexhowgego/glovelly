using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Glovelly.Api.Services;

public static class McpToolDocumentationGenerator
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase),
        },
    };

    public static string GenerateMarkdown()
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Glovelly MCP Tools");
        builder.AppendLine();
        builder.AppendLine("This file is generated from `GlovellyMcpToolCatalog`. Do not edit it by hand.");
        builder.AppendLine();
        builder.AppendLine("Glovelly exposes authenticated MCP tools for read-only business queries and staged gig imports. Staged-write tools create reviewable draft data only; they do not create real gigs until a user reviews and commits them in Glovelly.");

        foreach (var tool in GlovellyMcpToolCatalog.Tools)
        {
            builder.AppendLine();
            builder.AppendLine($"## `{tool.Name}`");
            builder.AppendLine();
            builder.AppendLine(tool.Description);
            builder.AppendLine();
            builder.AppendLine($"Title: {tool.Title}");
            builder.AppendLine();
            builder.AppendLine($"Safety level: {tool.SafetyLevel}");
            builder.AppendLine();
            builder.AppendLine($"Requires explicit user intent: {FormatBoolean(tool.RequiresExplicitUserIntent)}");
            builder.AppendLine();
            AppendInputs(builder, tool.InputSchema);
            AppendExample(builder, tool.InputSchema);
            AppendSchema(builder, "Input schema", tool.InputSchema);

            if (tool.OutputSchema is not null)
            {
                AppendSchema(builder, "Output schema", tool.OutputSchema);
            }
        }

        return builder.ToString();
    }

    public static string GenerateCapabilityManifestJson()
    {
        var manifest = new
        {
            name = "glovelly-mcp-tools",
            version = 1,
            tools = GlovellyMcpToolCatalog.Tools.Select(tool => new
            {
                tool.Name,
                tool.Title,
                tool.Description,
                tool.SafetyLevel,
                tool.RequiresExplicitUserIntent,
                tool.InputSchema,
                tool.OutputSchema,
            }),
        };

        return ToCanonicalJson(manifest) + "\n";
    }

    public static string ToCanonicalJson(object value)
    {
        var json = JsonSerializer.Serialize(value, JsonOptions);
        using var document = JsonDocument.Parse(json);
        return ToCanonicalJson(document.RootElement);
    }

    private static string ToCanonicalJson(JsonElement element)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            WriteCanonicalJsonElement(writer, element);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void AppendInputs(StringBuilder builder, object schema)
    {
        var element = JsonSerializer.SerializeToElement(schema, JsonOptions);
        builder.AppendLine("Inputs:");

        if (!element.TryGetProperty("properties", out var properties) || !properties.EnumerateObject().Any())
        {
            builder.AppendLine();
            builder.AppendLine("None.");
            return;
        }

        var required = GetRequiredProperties(element);
        foreach (var property in properties.EnumerateObject().OrderBy(property => property.Name, StringComparer.Ordinal))
        {
            var requirement = required.Contains(property.Name) ? "required" : "optional";
            builder.AppendLine($"- `{property.Name}` ({requirement}): {DescribeSchema(property.Value)}");
        }
    }

    private static void AppendExample(StringBuilder builder, object schema)
    {
        var element = JsonSerializer.SerializeToElement(schema, JsonOptions);
        var example = CreateExample(element) ?? new SortedDictionary<string, object?>(StringComparer.Ordinal);

        builder.AppendLine();
        builder.AppendLine("Example input:");
        builder.AppendLine();
        builder.AppendLine("```json");
        builder.AppendLine(ToCanonicalJson(example));
        builder.AppendLine("```");
    }

    private static void AppendSchema(StringBuilder builder, string heading, object schema)
    {
        builder.AppendLine();
        builder.AppendLine($"{heading}:");
        builder.AppendLine();
        builder.AppendLine("```json");
        builder.AppendLine(ToCanonicalJson(schema));
        builder.AppendLine("```");
    }

    private static HashSet<string> GetRequiredProperties(JsonElement schema)
    {
        if (!schema.TryGetProperty("required", out var required) || required.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return required
            .EnumerateArray()
            .Select(value => value.GetString())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToHashSet(StringComparer.Ordinal)!;
    }

    private static string DescribeSchema(JsonElement schema)
    {
        var type = schema.TryGetProperty("type", out var typeElement) ? typeElement.GetString() ?? "value" : "value";
        var description = schema.TryGetProperty("description", out var descriptionElement)
            ? descriptionElement.GetString()
            : null;

        if (schema.TryGetProperty("enum", out var enumValues))
        {
            type += " = " + string.Join(" | ", enumValues.EnumerateArray().Select(value => value.GetString()));
        }
        else if (schema.TryGetProperty("format", out var format))
        {
            type += $" ({format.GetString()})";
        }

        return string.IsNullOrWhiteSpace(description) ? type : $"{type}. {description}";
    }

    private static object? CreateExample(JsonElement schema)
    {
        if (schema.TryGetProperty("enum", out var enumValues))
        {
            return enumValues.EnumerateArray().FirstOrDefault().GetString();
        }

        var type = schema.TryGetProperty("type", out var typeElement) ? typeElement.GetString() : null;
        return type switch
        {
            "object" => CreateObjectExample(schema),
            "array" => CreateArrayExample(schema),
            "number" => 123.45m,
            "integer" => 1,
            "boolean" => true,
            "string" => CreateStringExample(schema),
            _ => null,
        };
    }

    private static object CreateObjectExample(JsonElement schema)
    {
        var properties = new SortedDictionary<string, object?>(StringComparer.Ordinal);
        if (!schema.TryGetProperty("properties", out var propertySchemas))
        {
            return properties;
        }

        var required = GetRequiredProperties(schema);
        var propertiesToInclude = required.Count > 0
            ? propertySchemas.EnumerateObject().Where(property => required.Contains(property.Name))
            : propertySchemas.EnumerateObject().Take(3);

        foreach (var property in propertiesToInclude)
        {
            properties[property.Name] = CreateExample(property.Value);
        }

        return properties;
    }

    private static object[] CreateArrayExample(JsonElement schema)
    {
        if (!schema.TryGetProperty("items", out var items))
        {
            return [];
        }

        return [CreateExample(items)!];
    }

    private static string CreateStringExample(JsonElement schema)
    {
        if (schema.TryGetProperty("format", out var format))
        {
            return format.GetString() switch
            {
                "uuid" => "00000000-0000-0000-0000-000000000000",
                "date" => "2026-01-31",
                "time" => "19:30",
                "date-time" => "2026-01-31T19:30:00Z",
                _ => "string",
            };
        }

        return "string";
    }

    private static string FormatBoolean(bool value) => value ? "yes" : "no";

    private static void WriteCanonicalJsonElement(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject().OrderBy(property => property.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonicalJsonElement(writer, property.Value);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                {
                    WriteCanonicalJsonElement(writer, item);
                }
                writer.WriteEndArray();
                break;
            default:
                element.WriteTo(writer);
                break;
        }
    }
}
