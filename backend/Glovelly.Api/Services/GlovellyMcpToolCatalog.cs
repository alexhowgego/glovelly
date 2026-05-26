using System.Text.Json.Serialization;

namespace Glovelly.Api.Services;

public sealed record McpToolDefinition(
    string Name,
    string Title,
    string Description,
    object InputSchema,
    [property: JsonIgnore] McpToolSafetyLevel SafetyLevel,
    [property: JsonIgnore] bool RequiresExplicitUserIntent = false,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] object? OutputSchema = null);

public enum McpToolSafetyLevel
{
    ReadOnly,
    StagedWrite,
    DirectWrite,
    ExternalSideEffect,
}

public static class GlovellyMcpToolCatalog
{
    public static IReadOnlyList<McpToolDefinition> Tools { get; } =
    [
        new(
            "glovelly_search_contacts",
            "Search Contacts",
            "Search Glovelly contacts by name or email. Returns possible matches without guessing.",
            new
            {
                type = "object",
                properties = new
                {
                    query = new { type = "string" },
                },
            },
            McpToolSafetyLevel.ReadOnly,
            OutputSchema: ContactSearchOutputSchema()),
        new(
            "glovelly_list_invoices",
            "List Invoices",
            "List invoices by optional contact, status, date range, and date basis.",
            new
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
            McpToolSafetyLevel.ReadOnly,
            OutputSchema: InvoiceListOutputSchema()),
        new(
            "glovelly_get_invoice",
            "Get Invoice",
            "Fetch read-only invoice details for a single invoice.",
            new
            {
                type = "object",
                required = new[] { "invoiceId" },
                properties = new
                {
                    invoiceId = new { type = "string", format = "uuid" },
                },
            },
            McpToolSafetyLevel.ReadOnly,
            OutputSchema: InvoiceGetOutputSchema()),
        new(
            "glovelly_list_receipts",
            "List Receipts",
            "List read-only receipt and expense records by date range and optional status.",
            new
            {
                type = "object",
                properties = new
                {
                    fromDate = new { type = "string", format = "date" },
                    toDate = new { type = "string", format = "date" },
                    status = new { type = "string", description = "all, matched, or unmatched" },
                },
            },
            McpToolSafetyLevel.ReadOnly,
            OutputSchema: ReceiptListOutputSchema()),
        new(
            "glovelly_get_business_summary",
            "Get Business Summary",
            "Summarise invoice totals, paid totals, outstanding totals, expenses, and receipt counts for a period.",
            new
            {
                type = "object",
                properties = new
                {
                    fromDate = new { type = "string", format = "date" },
                    toDate = new { type = "string", format = "date" },
                },
            },
            McpToolSafetyLevel.ReadOnly,
            OutputSchema: BusinessSummaryOutputSchema()),
        new(
            "glovelly_create_gig_import_batch",
            "Create Gig Import Batch",
            "Create a staged gig import batch for later human review. This does not create real gigs.",
            new
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
            McpToolSafetyLevel.StagedWrite,
            RequiresExplicitUserIntent: true,
            OutputSchema: GigImportBatchCreateOutputSchema()),
        new(
            "glovelly_add_gig_import_draft",
            "Add Gig Import Draft",
            "Add one draft gig row to a staged import batch. Missing uncertain values are allowed.",
            GigImportDraftInputSchema(requiredBatchId: true),
            McpToolSafetyLevel.StagedWrite,
            RequiresExplicitUserIntent: true,
            OutputSchema: GigImportDraftAddOutputSchema()),
        new(
            "glovelly_add_gig_import_drafts",
            "Add Gig Import Drafts",
            "Add multiple draft gig rows to a staged import batch, returning per-row validation feedback.",
            new
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
            McpToolSafetyLevel.StagedWrite,
            RequiresExplicitUserIntent: true,
            OutputSchema: GigImportDraftBulkAddOutputSchema()),
        new(
            "glovelly_list_gig_import_batches",
            "List Gig Import Batches",
            "List staged gig import batches and their statuses.",
            new
            {
                type = "object",
                properties = new { },
            },
            McpToolSafetyLevel.ReadOnly,
            OutputSchema: GigImportBatchListOutputSchema()),
        new(
            "glovelly_get_gig_import_batch",
            "Get Gig Import Batch",
            "Fetch a staged gig import batch and its draft rows.",
            new
            {
                type = "object",
                required = new[] { "batchId" },
                properties = new
                {
                    batchId = new { type = "string", format = "uuid" },
                },
            },
            McpToolSafetyLevel.ReadOnly,
            OutputSchema: GigImportBatchGetOutputSchema()),
    ];

    private static object ContactSearchOutputSchema() => new
    {
        type = "object",
        properties = new
        {
            query = new { type = "string" },
            matches = new
            {
                type = "array",
                items = ContactMatchSchema(),
            },
        },
    };

    private static object InvoiceListOutputSchema() => new
    {
        type = "object",
        properties = new
        {
            ambiguous = new { type = "boolean" },
            message = new { type = "string" },
            matches = new
            {
                type = "array",
                items = ContactMatchSchema(),
            },
            invoices = new
            {
                type = "array",
                items = InvoiceSummarySchema(),
            },
            totalOutstanding = new { type = "number" },
            currency = new { type = "string" },
        },
    };

    private static object InvoiceGetOutputSchema() => new
    {
        type = "object",
        properties = new
        {
            found = new { type = "boolean" },
            invoice = InvoiceDetailSchema(),
        },
    };

    private static object ReceiptListOutputSchema() => new
    {
        type = "object",
        properties = new
        {
            receipts = new
            {
                type = "array",
                items = ReceiptSummarySchema(),
            },
            receiptCount = new { type = "integer" },
            unmatchedReceiptCount = new { type = "integer" },
            totalAmount = new { type = "number" },
            currency = new { type = "string" },
        },
    };

    private static object BusinessSummaryOutputSchema() => new
    {
        type = "object",
        properties = new
        {
            fromDate = DateSchema(),
            toDate = DateSchema(),
            invoiceTotal = new { type = "number" },
            paidTotal = new { type = "number" },
            outstandingTotal = new { type = "number" },
            expenseTotal = new { type = "number" },
            receiptCount = new { type = "integer" },
            unmatchedReceiptCount = new { type = "integer" },
            currency = new { type = "string" },
        },
    };

    private static object GigImportBatchCreateOutputSchema() => new
    {
        type = "object",
        properties = new
        {
            created = new { type = "boolean" },
            validationErrors = StringArraySchema(),
            batch = GigImportBatchSummarySchema(),
        },
    };

    private static object GigImportDraftAddOutputSchema() => new
    {
        type = "object",
        properties = GigImportDraftAddResultProperties(),
    };

    private static object GigImportDraftBulkAddOutputSchema() => new
    {
        type = "object",
        properties = new
        {
            batchFound = new { type = "boolean" },
            submittedCount = new { type = "integer" },
            createdCount = new { type = "integer" },
            results = new
            {
                type = "array",
                items = GigImportDraftAddOutputSchema(),
            },
        },
    };

    private static object GigImportBatchListOutputSchema() => new
    {
        type = "object",
        properties = new
        {
            batches = new
            {
                type = "array",
                items = GigImportBatchSummarySchema(),
            },
        },
    };

    private static object GigImportBatchGetOutputSchema() => new
    {
        type = "object",
        properties = new
        {
            found = new { type = "boolean" },
            batch = GigImportBatchDetailSchema(),
        },
    };

    private static object ContactMatchSchema() => new
    {
        type = "object",
        properties = new
        {
            contactId = UuidSchema(),
            name = new { type = "string" },
            email = new { type = "string" },
        },
    };

    private static object InvoiceSummarySchema() => new
    {
        type = "object",
        properties = new
        {
            invoiceId = UuidSchema(),
            invoiceNumber = new { type = "string" },
            contactId = UuidSchema(),
            contactName = new { type = "string" },
            issueDate = DateSchema(),
            dueDate = DateSchema(),
            status = new { type = "string" },
            total = new { type = "number" },
            outstandingAmount = new { type = "number" },
            currency = new { type = "string" },
        },
    };

    private static object InvoiceDetailSchema() => new
    {
        type = "object",
        properties = new
        {
            invoiceId = UuidSchema(),
            invoiceNumber = new { type = "string" },
            contactId = UuidSchema(),
            contactName = new { type = "string" },
            issueDate = DateSchema(),
            dueDate = DateSchema(),
            status = new { type = "string" },
            description = new { type = "string" },
            total = new { type = "number" },
            outstandingAmount = new { type = "number" },
            currency = new { type = "string" },
            lines = new
            {
                type = "array",
                items = new
                {
                    type = "object",
                    properties = new
                    {
                        invoiceLineId = UuidSchema(),
                        description = new { type = "string" },
                        quantity = new { type = "number" },
                        unitPrice = new { type = "number" },
                        lineTotal = new { type = "number" },
                        type = new { type = "string" },
                        gigId = UuidSchema(),
                    },
                },
            },
        },
    };

    private static object ReceiptSummarySchema() => new
    {
        type = "object",
        properties = new
        {
            receiptId = UuidSchema(),
            gigId = UuidSchema(),
            gigTitle = new { type = "string" },
            receiptDate = DateSchema(),
            contactId = UuidSchema(),
            contactName = new { type = "string" },
            description = new { type = "string" },
            amount = new { type = "number" },
            status = new { type = "string" },
            attachmentCount = new { type = "integer" },
            attachmentFileNames = StringArraySchema(),
            currency = new { type = "string" },
        },
    };

    private static object GigImportBatchSummarySchema() => new
    {
        type = "object",
        properties = new
        {
            batchId = UuidSchema(),
            sourceName = new { type = "string" },
            sourceFingerprint = new { type = "string" },
            status = new { type = "string" },
            createdAtUtc = DateTimeSchema(),
            notes = new { type = "string" },
            draftCount = new { type = "integer" },
        },
    };

    private static object GigImportBatchDetailSchema() => new
    {
        type = "object",
        properties = new
        {
            batchId = UuidSchema(),
            sourceName = new { type = "string" },
            sourceFingerprint = new { type = "string" },
            status = new { type = "string" },
            createdAtUtc = DateTimeSchema(),
            notes = new { type = "string" },
            draftCount = new { type = "integer" },
            drafts = new
            {
                type = "array",
                items = GigImportDraftDetailSchema(),
            },
        },
    };

    private static object GigImportDraftDetailSchema() => new
    {
        type = "object",
        properties = new
        {
            draftId = UuidSchema(),
            batchId = UuidSchema(),
            proposedClientId = UuidSchema(),
            clientName = new { type = "string" },
            contactName = new { type = "string" },
            contactEmail = new { type = "string" },
            projectName = new { type = "string" },
            title = new { type = "string" },
            date = DateSchema(),
            arrivalTime = TimeSchema(),
            rehearsalStartTime = TimeSchema(),
            rehearsalEndTime = TimeSchema(),
            showStartTime = TimeSchema(),
            showEndTime = TimeSchema(),
            venueName = new { type = "string" },
            venueAddress = new { type = "string" },
            postcode = new { type = "string" },
            fee = new { type = "number" },
            perDiem = new { type = "number" },
            notes = new { type = "string" },
            accommodationNotes = new { type = "string" },
            travelNotes = new { type = "string" },
            sourceReference = new { type = "string" },
            confidence = new { type = "string" },
            warnings = StringArraySchema(),
            status = new { type = "string" },
        },
    };

    private static object GigImportDraftAddResultProperties() => new
    {
        batchFound = new { type = "boolean" },
        created = new { type = "boolean" },
        index = new { type = "integer" },
        validationErrors = StringArraySchema(),
        contactMatches = new
        {
            type = "array",
            items = ContactMatchSchema(),
        },
        draft = GigImportDraftDetailSchema(),
    };

    private static object StringArraySchema() => new
    {
        type = "array",
        items = new { type = "string" },
    };

    private static object UuidSchema() => new { type = "string", format = "uuid" };

    private static object DateSchema() => new
    {
        type = "string",
        format = "date",
        description = "ISO 8601 calendar date in YYYY-MM-DD format.",
    };

    private static object TimeSchema() => new
    {
        type = "string",
        format = "time",
        description = "Local time in HH:mm format.",
    };

    private static object DateTimeSchema() => new
    {
        type = "string",
        format = "date-time",
        description = "ISO 8601 UTC timestamp.",
    };

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
}
