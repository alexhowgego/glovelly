using System.Text.Json.Serialization;
using Glovelly.Api.Models;

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
            McpSchema.Object(new
            {
                query = McpSchema.String("Name or email text to search for. Leave blank to list recent contacts."),
            }),
            McpToolSafetyLevel.ReadOnly,
            OutputSchema: ContactSearchOutputSchema()),
        new(
            "glovelly_list_invoices",
            "List Invoices",
            "List invoices by optional contact, status, date range, and date basis.",
            McpSchema.Object(MergeProperties(
                GlovellyMcpSchemaFragments.ContactSelector,
                GlovellyMcpSchemaFragments.DateRange,
                new Dictionary<string, object>
                {
                    ["status"] = McpSchema.Enum(
                        ["all", "outstanding", "issued", "paid", "draft", "overdue", "cancelled"],
                        "Invoice status filter. Use outstanding for issued or overdue invoices with a balance."),
                    ["dateBasis"] = McpSchema.Enum<InvoiceDateBasis>("Whether fromDate/toDate apply to issueDate or dueDate."),
                })),
            McpToolSafetyLevel.ReadOnly,
            OutputSchema: InvoiceListOutputSchema()),
        new(
            "glovelly_get_invoice",
            "Get Invoice",
            "Fetch read-only invoice details for a single invoice.",
            McpSchema.Object(new
            {
                invoiceId = McpSchema.Uuid("Invoice ID returned by glovelly_list_invoices."),
            }, ["invoiceId"]),
            McpToolSafetyLevel.ReadOnly,
            OutputSchema: InvoiceGetOutputSchema()),
        new(
            "glovelly_list_receipts",
            "List Receipts",
            "List read-only receipt and expense records by date range and optional status.",
            McpSchema.Object(MergeProperties(
                GlovellyMcpSchemaFragments.DateRange,
                new Dictionary<string, object>
                {
                    ["status"] = McpSchema.Enum(
                        ["all", ReceiptStatusValues.Matched, ReceiptStatusValues.Unmatched],
                        "Receipt matching status filter."),
                })),
            McpToolSafetyLevel.ReadOnly,
            OutputSchema: ReceiptListOutputSchema()),
        new(
            "glovelly_get_business_summary",
            "Get Business Summary",
            "Summarise invoice totals, paid totals, outstanding totals, expenses, and receipt counts for a period.",
            McpSchema.Object(GlovellyMcpSchemaFragments.DateRange),
            McpToolSafetyLevel.ReadOnly,
            OutputSchema: BusinessSummaryOutputSchema()),
        new(
            "glovelly_create_gig_import_batch",
            "Create Gig Import Batch",
            "Create a staged gig import batch for later human review. This does not create real gigs.",
            McpSchema.Object(new
            {
                sourceName = McpSchema.String("Human-readable source being imported, such as an email subject or document name.", maxLength: 300),
                notes = McpSchema.String("Optional notes about the source or import assumptions.", maxLength: 4000),
                sourceFingerprint = McpSchema.String("Optional stable source identifier to help detect duplicate imports.", maxLength: 200),
            }, ["sourceName"]),
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
            McpSchema.Object(new
            {
                batchId = McpSchema.Uuid("Staged gig import batch ID returned by glovelly_create_gig_import_batch."),
                drafts = McpSchema.Array(GigImportDraftInputSchema(requiredBatchId: false), "Draft gig rows to add to the staged import batch."),
            }, ["batchId", "drafts"]),
            McpToolSafetyLevel.StagedWrite,
            RequiresExplicitUserIntent: true,
            OutputSchema: GigImportDraftBulkAddOutputSchema()),
        new(
            "glovelly_list_gig_import_batches",
            "List Gig Import Batches",
            "List staged gig import batches and their statuses.",
            McpSchema.Object(new { }),
            McpToolSafetyLevel.ReadOnly,
            OutputSchema: GigImportBatchListOutputSchema()),
        new(
            "glovelly_get_gig_import_batch",
            "Get Gig Import Batch",
            "Fetch a staged gig import batch and its draft rows.",
            McpSchema.Object(new
            {
                batchId = McpSchema.Uuid("Staged gig import batch ID returned by glovelly_list_gig_import_batches."),
            }, ["batchId"]),
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
            totalOutstanding = McpSchema.Money("Total outstanding amount across returned invoices."),
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
            totalAmount = McpSchema.Money("Total receipt and expense amount across returned records."),
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
            invoiceTotal = McpSchema.Money("Invoice total for the period."),
            paidTotal = McpSchema.Money("Paid invoice total for the period."),
            outstandingTotal = McpSchema.Money("Outstanding invoice total for the period."),
            expenseTotal = McpSchema.Money("Expense total for the period."),
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
            status = McpSchema.Enum<InvoiceStatus>("Invoice lifecycle status."),
            total = McpSchema.Money("Invoice total amount."),
            outstandingAmount = McpSchema.Money("Amount still outstanding on this invoice."),
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
            status = McpSchema.Enum<InvoiceStatus>("Invoice lifecycle status."),
            description = new { type = "string" },
            total = McpSchema.Money("Invoice total amount."),
            outstandingAmount = McpSchema.Money("Amount still outstanding on this invoice."),
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
                        unitPrice = McpSchema.Money("Unit price for this invoice line."),
                        lineTotal = McpSchema.Money("Total amount for this invoice line."),
                        type = McpSchema.Enum<InvoiceLineType>("Invoice line type."),
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
            amount = McpSchema.Money("Receipt or expense amount."),
            status = McpSchema.Enum([ReceiptStatusValues.Matched, ReceiptStatusValues.Unmatched], "Receipt matching status."),
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
            status = McpSchema.Enum<GigImportBatchStatus>("Staged import batch status."),
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
            status = McpSchema.Enum<GigImportBatchStatus>("Staged import batch status."),
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
            fee = McpSchema.Money("Proposed gig fee."),
            perDiem = McpSchema.Money("Proposed per diem amount."),
            notes = new { type = "string" },
            accommodationNotes = new { type = "string" },
            travelNotes = new { type = "string" },
            sourceReference = new { type = "string" },
            confidence = GlovellyMcpSchemaFragments.Confidence,
            warnings = StringArraySchema(),
            status = McpSchema.Enum<GigImportDraftStatus>("Draft review status."),
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

    private static object StringArraySchema() => McpSchema.Array(McpSchema.String());

    private static object UuidSchema() => McpSchema.Uuid();

    private static object DateSchema() => McpSchema.Date();

    private static object TimeSchema() => McpSchema.Time();

    private static object DateTimeSchema() => McpSchema.DateTime();

    private static Dictionary<string, object> MergeProperties(params object[] fragments)
    {
        var merged = new Dictionary<string, object>();
        foreach (var fragment in fragments)
        {
            if (fragment is not IReadOnlyDictionary<string, object> properties)
            {
                throw new ArgumentException("MCP schema property fragments must be dictionaries.", nameof(fragments));
            }

            foreach (var property in properties)
            {
                merged[property.Key] = property.Value;
            }
        }

        return merged;
    }

    private static object GigImportDraftInputSchema(bool requiredBatchId)
    {
        var required = requiredBatchId ? new[] { "batchId" } : [];
        var properties = new Dictionary<string, object?>
        {
            ["title"] = McpSchema.String("Gig title or role summary.", maxLength: 200),
            ["clientName"] = McpSchema.String("Client or booker name as found in the source.", maxLength: 200),
            ["contactQuery"] = McpSchema.String("Name or email text to resolve the gig client/contact."),
            ["contactName"] = McpSchema.String("Contact person name as found in the source.", maxLength: 200),
            ["contactEmail"] = McpSchema.String("Contact email address as found in the source.", maxLength: 320),
            ["projectName"] = McpSchema.String("Project, production, tour, or engagement name.", maxLength: 200),
            ["date"] = McpSchema.Date(),
            ["arrivalTime"] = McpSchema.Time(),
            ["rehearsalStartTime"] = McpSchema.Time(),
            ["rehearsalEndTime"] = McpSchema.Time(),
            ["showStartTime"] = McpSchema.Time(),
            ["showEndTime"] = McpSchema.Time(),
            ["venueName"] = McpSchema.String("Venue name.", maxLength: 200),
            ["venueAddress"] = McpSchema.String("Venue address.", maxLength: 1000),
            ["postcode"] = McpSchema.String("Venue postcode.", maxLength: 20),
            ["fee"] = McpSchema.Money("Proposed gig fee.", minimum: 0),
            ["perDiem"] = McpSchema.Money("Proposed per diem amount.", minimum: 0),
            ["notes"] = McpSchema.String("General notes from the source.", maxLength: 4000),
            ["accommodationNotes"] = McpSchema.String("Accommodation details or uncertainty.", maxLength: 4000),
            ["travelNotes"] = McpSchema.String("Travel details or uncertainty.", maxLength: 4000),
            ["sourceReference"] = McpSchema.String("Optional reference to the source row, page, message, or attachment.", maxLength: 500),
            ["confidence"] = GlovellyMcpSchemaFragments.Confidence,
            ["warnings"] = StringArraySchema(),
        };

        if (requiredBatchId)
        {
            properties["batchId"] = McpSchema.Uuid("Staged gig import batch ID returned by glovelly_create_gig_import_batch.");
        }

        return McpSchema.Object(properties, required);
    }
}
