using System.Text.Json;
using System.Text.Json.Serialization;
using Glovelly.Api.Auth;
using Glovelly.Api.Services;

namespace Glovelly.Api.Endpoints;

public static class McpEndpoints
{
    private const string ProtocolVersion = "2025-06-18";
    private const string LegacyProtocolVersion = "2024-11-05";
    private const string McpProtocolVersionHeader = "MCP-Protocol-Version";
    private const string McpSessionIdHeader = "Mcp-Session-Id";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters =
        {
            new LenientNullableDateOnlyConverter(),
            new LenientNullableTimeOnlyConverter(),
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase),
        },
    };

    public static IEndpointRouteBuilder MapMcpEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapMethods("/mcp", ["OPTIONS"], HandlePreflight)
            .AllowAnonymous()
            .WithTags("MCP");

        app.MapGet("/mcp", (HttpContext httpContext) =>
            {
                SetMcpCorsHeaders(httpContext.Response);
                httpContext.Response.Headers["Allow"] = "POST, OPTIONS";
                httpContext.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
                return Task.CompletedTask;
            })
            .AllowAnonymous()
            .WithTags("MCP");

        app.MapDelete("/mcp", (HttpContext httpContext) =>
            {
                SetMcpCorsHeaders(httpContext.Response);
                httpContext.Response.Headers["Allow"] = "POST, OPTIONS";
                httpContext.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
                return Task.CompletedTask;
            })
            .AllowAnonymous()
            .WithTags("MCP");

        app.MapPost("/mcp", HandleRpcAsync)
            .RequireAuthorization(GlovellyPolicies.McpUser)
            .WithTags("MCP");

        return app;
    }

    private static IResult HandlePreflight(HttpContext httpContext)
    {
        SetMcpCorsHeaders(httpContext.Response);
        httpContext.Response.Headers.AccessControlAllowMethods = "POST, GET, DELETE, OPTIONS";
        httpContext.Response.Headers.AccessControlAllowHeaders =
            "accept, authorization, content-type, last-event-id, mcp-session-id, mcp-protocol-version";
        httpContext.Response.Headers.AccessControlMaxAge = "86400";

        return Results.NoContent();
    }

    private static async Task<IResult> HandleRpcAsync(
        JsonElement request,
        HttpContext httpContext,
        ICurrentUserAccessor currentUserAccessor,
        IGlovellyMcpQueryService queryService,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        SetMcpCorsHeaders(httpContext.Response);

        var versionValidation = ValidateProtocolVersion(httpContext.Request);
        if (versionValidation is not null)
        {
            return versionValidation;
        }

        if (!request.TryGetProperty("id", out var id))
        {
            return Results.Accepted();
        }

        var logger = loggerFactory.CreateLogger("Glovelly.Mcp");
        var userId = currentUserAccessor.TryGetUserId(httpContext.User);
        if (!userId.HasValue)
        {
            return Results.Unauthorized();
        }

        var method = request.TryGetProperty("method", out var methodElement)
            ? methodElement.GetString()
            : null;

        logger.LogInformation("Glovelly MCP method {Method} called by user {UserId}.", method, userId);

        try
        {
            return method switch
            {
                "initialize" => Rpc(id, result: InitializeResult(request)),
                "ping" => Rpc(id, result: new { }),
                "tools/list" => Rpc(id, result: new { tools = Tools }),
                "tools/call" => await CallToolAsync(id, request, userId.Value, queryService, logger, cancellationToken),
                _ => Rpc(id, error: new McpRpcError(-32601, $"Unsupported MCP method '{method}'.")),
            };
        }
        catch (JsonException exception)
        {
            logger.LogWarning(exception, "Invalid Glovelly MCP request payload.");
            return Rpc(id, error: new McpRpcError(-32602, "Invalid tool arguments."));
        }
    }

    private static IResult? ValidateProtocolVersion(HttpRequest request)
    {
        if (!request.Headers.TryGetValue(McpProtocolVersionHeader, out var values))
        {
            return null;
        }

        var version = values.FirstOrDefault();
        return version is ProtocolVersion or LegacyProtocolVersion
            ? null
            : Results.BadRequest(new
            {
                error = $"Unsupported MCP protocol version '{version}'.",
                supported = new[] { ProtocolVersion, LegacyProtocolVersion },
            });
    }

    private static void SetMcpCorsHeaders(HttpResponse response)
    {
        response.Headers.AccessControlAllowOrigin = "*";
        response.Headers.AccessControlExposeHeaders = $"{McpSessionIdHeader}, {McpProtocolVersionHeader}, WWW-Authenticate";
        response.Headers[McpProtocolVersionHeader] = ProtocolVersion;
    }

    private static async Task<IResult> CallToolAsync(
        JsonElement id,
        JsonElement request,
        Guid userId,
        IGlovellyMcpQueryService queryService,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        if (!request.TryGetProperty("params", out var parameters) ||
            !parameters.TryGetProperty("name", out var nameElement))
        {
            return Rpc(id, error: new McpRpcError(-32602, "Tool name is required."));
        }

        var toolName = nameElement.GetString();
        var arguments = parameters.TryGetProperty("arguments", out var argumentsElement)
            ? argumentsElement
            : default;

        logger.LogInformation("Glovelly MCP tool {ToolName} called by user {UserId}.", toolName, userId);

        object? result = toolName switch
        {
            "glovelly_search_contacts" => await queryService.SearchContactsAsync(
                userId,
                ReadArgument<string?>(arguments, "query"),
                cancellationToken),
            "glovelly_list_invoices" => await queryService.ListInvoicesAsync(
                userId,
                ReadArguments<InvoiceListRequest>(arguments),
                cancellationToken),
            "glovelly_get_invoice" => await GetInvoiceAsync(userId, arguments, queryService, cancellationToken),
            "glovelly_list_receipts" => await queryService.ListReceiptsAsync(
                userId,
                ReadArguments<ReceiptListRequest>(arguments),
                cancellationToken),
            "glovelly_get_business_summary" => await queryService.GetBusinessSummaryAsync(
                userId,
                ReadArguments<BusinessSummaryRequest>(arguments),
                cancellationToken),
            "glovelly_create_gig_import_batch" => await queryService.CreateGigImportBatchAsync(
                userId,
                ReadArguments<GigImportBatchCreateRequest>(arguments),
                cancellationToken),
            "glovelly_add_gig_import_draft" => await queryService.AddGigImportDraftAsync(
                userId,
                ReadArguments<GigImportDraftAddRequest>(arguments),
                cancellationToken),
            "glovelly_add_gig_import_drafts" => await queryService.AddGigImportDraftsAsync(
                userId,
                ReadArguments<GigImportDraftBulkAddRequest>(arguments),
                cancellationToken),
            "glovelly_list_gig_import_batches" => await queryService.ListGigImportBatchesAsync(
                userId,
                cancellationToken),
            "glovelly_get_gig_import_batch" => await GetGigImportBatchAsync(userId, arguments, queryService, cancellationToken),
            _ => null,
        };

        if (result is null)
        {
            return Rpc(id, error: new McpRpcError(-32602, $"Unknown or unavailable tool '{toolName}'."));
        }

        return Rpc(id, result: ToolResult(result));
    }

    private static async Task<object?> GetInvoiceAsync(
        Guid userId,
        JsonElement arguments,
        IGlovellyMcpQueryService queryService,
        CancellationToken cancellationToken)
    {
        var invoiceId = ReadArgument<Guid?>(arguments, "invoiceId");
        if (!invoiceId.HasValue || invoiceId == Guid.Empty)
        {
            return null;
        }

        var invoice = await queryService.GetInvoiceAsync(userId, invoiceId.Value, cancellationToken);
        return new
        {
            found = invoice is not null,
            invoice,
        };
    }

    private static async Task<object?> GetGigImportBatchAsync(
        Guid userId,
        JsonElement arguments,
        IGlovellyMcpQueryService queryService,
        CancellationToken cancellationToken)
    {
        var batchId = ReadArgument<Guid?>(arguments, "batchId");
        if (!batchId.HasValue || batchId == Guid.Empty)
        {
            return null;
        }

        return await queryService.GetGigImportBatchAsync(userId, batchId.Value, cancellationToken);
    }

    private static T ReadArguments<T>(JsonElement arguments)
    {
        if (arguments.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return JsonSerializer.Deserialize<T>("{}", JsonOptions)!;
        }

        return arguments.Deserialize<T>(JsonOptions)!;
    }

    private static T ReadArgument<T>(JsonElement arguments, string name)
    {
        if (arguments.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null ||
            !arguments.TryGetProperty(name, out var value))
        {
            return default!;
        }

        return value.Deserialize<T>(JsonOptions)!;
    }

    private static IResult Rpc(JsonElement id, object? result = null, McpRpcError? error = null)
    {
        var response = error is null
            ? new McpRpcResponse("2.0", id, result, null)
            : new McpRpcResponse("2.0", id, null, error);

        return Results.Json(response, JsonOptions);
    }

    private static object InitializeResult(JsonElement request)
    {
        var requestedVersion = request.TryGetProperty("params", out var parameters) &&
            parameters.TryGetProperty("protocolVersion", out var versionElement)
                ? versionElement.GetString()
                : null;
        var protocolVersion = requestedVersion is LegacyProtocolVersion
            ? LegacyProtocolVersion
            : ProtocolVersion;

        return new
        {
            protocolVersion,
            serverInfo = new
            {
                name = "glovelly",
                title = "Glovelly",
                version = "0.1.0-experimental",
            },
            capabilities = new
            {
                tools = new
                {
                    listChanged = false,
                },
            },
        };
    }

    private static object ToolResult(object structuredContent)
    {
        return new
        {
            content = new[]
            {
                new
                {
                    type = "text",
                    text = JsonSerializer.Serialize(structuredContent, JsonOptions),
                },
            },
            structuredContent,
            isError = false,
        };
    }

    private static readonly object[] Tools =
    [
        new
        {
            name = "glovelly_search_contacts",
            title = "Search Contacts",
            description = "Search Glovelly contacts by name or email. Returns possible matches without guessing.",
            inputSchema = new
            {
                type = "object",
                properties = new
                {
                    query = new { type = "string" },
                },
            },
        },
        new
        {
            name = "glovelly_list_invoices",
            title = "List Invoices",
            description = "List invoices by optional contact, status, date range, and date basis.",
            inputSchema = new
            {
                type = "object",
                properties = new
                {
                    contactId = new { type = "string", format = "uuid" },
                    contactQuery = new { type = "string" },
                    status = new { type = "string", description = "all, outstanding, issued, paid, draft, overdue, or cancelled" },
                    fromDate = new { type = "string", format = "date" },
                    toDate = new { type = "string", format = "date" },
                    dateBasis = new { type = "string", @enum = new[] { "issueDate", "dueDate" } },
                },
            },
        },
        new
        {
            name = "glovelly_get_invoice",
            title = "Get Invoice",
            description = "Fetch read-only invoice details for a single invoice.",
            inputSchema = new
            {
                type = "object",
                required = new[] { "invoiceId" },
                properties = new
                {
                    invoiceId = new { type = "string", format = "uuid" },
                },
            },
        },
        new
        {
            name = "glovelly_list_receipts",
            title = "List Receipts",
            description = "List read-only receipt and expense records by date range and optional status.",
            inputSchema = new
            {
                type = "object",
                properties = new
                {
                    fromDate = new { type = "string", format = "date" },
                    toDate = new { type = "string", format = "date" },
                    status = new { type = "string", description = "all, matched, or unmatched" },
                },
            },
        },
        new
        {
            name = "glovelly_get_business_summary",
            title = "Get Business Summary",
            description = "Summarise invoice totals, paid totals, outstanding totals, expenses, and receipt counts for a period.",
            inputSchema = new
            {
                type = "object",
                properties = new
                {
                    fromDate = new { type = "string", format = "date" },
                    toDate = new { type = "string", format = "date" },
                },
            },
        },
        new
        {
            name = "glovelly_create_gig_import_batch",
            title = "Create Gig Import Batch",
            description = "Create a staged gig import batch for later human review. This does not create real gigs.",
            inputSchema = new
            {
                type = "object",
                required = new[] { "sourceName" },
                properties = new
                {
                    sourceName = new { type = "string", maxLength = 300 },
                    notes = new { type = "string", maxLength = 4000 },
                    sourceFingerprint = new { type = "string", maxLength = 200 },
                },
            },
        },
        new
        {
            name = "glovelly_add_gig_import_draft",
            title = "Add Gig Import Draft",
            description = "Add one draft gig row to a staged import batch. Missing uncertain values are allowed.",
            inputSchema = GigImportDraftInputSchema(requiredBatchId: true),
        },
        new
        {
            name = "glovelly_add_gig_import_drafts",
            title = "Add Gig Import Drafts",
            description = "Add multiple draft gig rows to a staged import batch, returning per-row validation feedback.",
            inputSchema = new
            {
                type = "object",
                required = new[] { "batchId", "drafts" },
                properties = new
                {
                    batchId = new { type = "string", format = "uuid" },
                    drafts = new
                    {
                        type = "array",
                        items = GigImportDraftInputSchema(requiredBatchId: false),
                    },
                },
            },
        },
        new
        {
            name = "glovelly_list_gig_import_batches",
            title = "List Gig Import Batches",
            description = "List staged gig import batches and their statuses.",
            inputSchema = new
            {
                type = "object",
                properties = new { },
            },
        },
        new
        {
            name = "glovelly_get_gig_import_batch",
            title = "Get Gig Import Batch",
            description = "Fetch a staged gig import batch and its draft rows.",
            inputSchema = new
            {
                type = "object",
                required = new[] { "batchId" },
                properties = new
                {
                    batchId = new { type = "string", format = "uuid" },
                },
            },
        },
    ];

    private static object GigImportDraftInputSchema(bool requiredBatchId)
    {
        var required = requiredBatchId ? new[] { "batchId" } : [];
        var properties = new Dictionary<string, object?>
        {
            ["title"] = new { type = "string", maxLength = 200 },
            ["clientName"] = new { type = "string", maxLength = 200 },
            ["contactQuery"] = new { type = "string" },
            ["contactName"] = new { type = "string", maxLength = 200 },
            ["contactEmail"] = new { type = "string", maxLength = 320 },
            ["projectName"] = new { type = "string", maxLength = 200 },
            ["date"] = new { type = "string", format = "date" },
            ["arrivalTime"] = new { type = "string", format = "time" },
            ["rehearsalStartTime"] = new { type = "string", format = "time" },
            ["rehearsalEndTime"] = new { type = "string", format = "time" },
            ["showStartTime"] = new { type = "string", format = "time" },
            ["showEndTime"] = new { type = "string", format = "time" },
            ["venueName"] = new { type = "string", maxLength = 200 },
            ["venueAddress"] = new { type = "string", maxLength = 1000 },
            ["postcode"] = new { type = "string", maxLength = 20 },
            ["fee"] = new { type = "number", minimum = 0 },
            ["perDiem"] = new { type = "number", minimum = 0 },
            ["notes"] = new { type = "string", maxLength = 4000 },
            ["accommodationNotes"] = new { type = "string", maxLength = 4000 },
            ["travelNotes"] = new { type = "string", maxLength = 4000 },
            ["sourceReference"] = new { type = "string", maxLength = 500 },
            ["confidence"] = new { type = "string", @enum = new[] { "low", "medium", "high" } },
            ["warnings"] = new
            {
                type = "array",
                items = new { type = "string" },
            },
        };

        if (requiredBatchId)
        {
            properties["batchId"] = new { type = "string", format = "uuid" };
        }

        return new
        {
            type = "object",
            required,
            properties,
        };
    }

    private sealed record McpRpcResponse(
        [property: JsonPropertyName("jsonrpc")] string JsonRpc,
        JsonElement Id,
        object? Result,
        McpRpcError? Error);

    private sealed record McpRpcError(int Code, string Message);

    private sealed class LenientNullableDateOnlyConverter : JsonConverter<DateOnly?>
    {
        private static readonly string[] DateFormats =
        [
            "yyyy-MM-dd",
            "yyyy/MM/dd",
            "dd/MM/yyyy",
            "d/M/yyyy",
            "dd-MM-yyyy",
            "d-M-yyyy",
            "d MMM yyyy",
            "dd MMM yyyy",
            "d MMMM yyyy",
            "dd MMMM yyyy",
            "ddd d MMM yyyy",
            "dddd d MMM yyyy",
            "ddd dd MMM yyyy",
            "dddd dd MMM yyyy",
        ];

        public override DateOnly? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return null;
            }

            if (reader.TokenType != JsonTokenType.String)
            {
                reader.Skip();
                return null;
            }

            var value = reader.GetString();
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            if (DateOnly.TryParseExact(
                    value.Trim(),
                    DateFormats,
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.AllowWhiteSpaces,
                    out var date))
            {
                return date;
            }

            return DateOnly.TryParse(
                value.Trim(),
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AllowWhiteSpaces,
                out date)
                ? date
                : null;
        }

        public override void Write(Utf8JsonWriter writer, DateOnly? value, JsonSerializerOptions options)
        {
            if (value.HasValue)
            {
                writer.WriteStringValue(value.Value.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
                return;
            }

            writer.WriteNullValue();
        }
    }

    private sealed class LenientNullableTimeOnlyConverter : JsonConverter<TimeOnly?>
    {
        private static readonly string[] TimeFormats =
        [
            "HH:mm",
            "H:mm",
            "HH:mm:ss",
            "H:mm:ss",
            "h:mm tt",
            "hh:mm tt",
            "htt",
            "h tt",
        ];

        public override TimeOnly? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return null;
            }

            if (reader.TokenType != JsonTokenType.String)
            {
                reader.Skip();
                return null;
            }

            var value = reader.GetString();
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            if (TimeOnly.TryParseExact(
                    value.Trim(),
                    TimeFormats,
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.AllowWhiteSpaces,
                    out var time))
            {
                return time;
            }

            return TimeOnly.TryParse(
                value.Trim(),
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AllowWhiteSpaces,
                out time)
                ? time
                : null;
        }

        public override void Write(Utf8JsonWriter writer, TimeOnly? value, JsonSerializerOptions options)
        {
            if (value.HasValue)
            {
                writer.WriteStringValue(value.Value.ToString("HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture));
                return;
            }

            writer.WriteNullValue();
        }
    }
}
