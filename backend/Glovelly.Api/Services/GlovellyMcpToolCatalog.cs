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
            "glovelly_get_contact",
            "Get Contact",
            "Fetch read-only contact details and related summary counts for one contact.",
            McpSchema.Object(new
            {
                contactId = McpSchema.Uuid("Contact ID returned by glovelly_search_contacts."),
            }, ["contactId"]),
            McpToolSafetyLevel.ReadOnly,
            OutputSchema: ContactGetOutputSchema()),
        new(
            "glovelly_list_gigs",
            "List Gigs",
            "List gigs by optional contact, status, type, date range, and invoicing state.",
            McpSchema.Object(MergeProperties(
                GlovellyMcpSchemaFragments.ContactSelector,
                GlovellyMcpSchemaFragments.DateRange,
                GlovellyMcpSchemaFragments.GigFilters)),
            McpToolSafetyLevel.ReadOnly,
            OutputSchema: GigListOutputSchema()),
        new(
            "glovelly_get_gig",
            "Get Gig",
            "Fetch read-only details for a single gig.",
            McpSchema.Object(new
            {
                gigId = McpSchema.Uuid("Gig ID returned by glovelly_list_gigs."),
            }, ["gigId"]),
            McpToolSafetyLevel.ReadOnly,
            OutputSchema: GigGetOutputSchema()),
        new(
            "glovelly_list_uninvoiced_gigs",
            "List Uninvoiced Gigs",
            "List visible gigs that are not linked to an invoice.",
            McpSchema.Object(MergeProperties(
                GlovellyMcpSchemaFragments.ContactSelector,
                GlovellyMcpSchemaFragments.DateRange,
                new Dictionary<string, object>
                {
                        ["status"] = McpSchema.Enum(
                        ["all", "draft", "confirmed", "planned", "completed", "cancelled", "canceled"],
                            "Gig status filter. Use confirmed or planned for planned gigs."),
                    ["gigType"] = McpSchema.Enum<GigType>("Nature of the musician work."),
                })),
            McpToolSafetyLevel.ReadOnly,
            OutputSchema: GigListOutputSchema()),
        new(
            "glovelly_list_gig_resources",
            "List Gig Resources",
            "List read-only metadata for links and files attached to a gig. File contents are not returned.",
            McpSchema.Object(new
            {
                gigId = McpSchema.Uuid("Gig ID returned by glovelly_list_gigs."),
            }, ["gigId"]),
            McpToolSafetyLevel.ReadOnly,
            OutputSchema: GigResourceListOutputSchema()),
        new(
            "glovelly_get_gig_setlist",
            "Get Gig Setlist",
            "Fetch the active setlist import already stored for a gig without reading Google Sheets.",
            McpSchema.Object(new
            {
                gigId = McpSchema.Uuid("Gig ID returned by glovelly_list_gigs."),
            }, ["gigId"]),
            McpToolSafetyLevel.ReadOnly,
            OutputSchema: GigSetlistGetOutputSchema()),
        new(
            "glovelly_preview_expense_statement",
            "Preview Expense Statement",
            "Build a read-only structured expense statement preview without generating PDFs or sending it anywhere.",
            McpSchema.Object(new
            {
                contactId = McpSchema.Uuid("Contact ID for the statement client."),
                gigIds = McpSchema.Array(McpSchema.Uuid(), "Optional gig IDs to include."),
                expenseIds = McpSchema.Array(McpSchema.Uuid(), "Optional expense IDs to include."),
                includeReceiptAttachments = new { type = "boolean", description = "Whether receipt attachment metadata should be considered by the statement builder." },
                includeReceiptAppendix = new { type = "boolean", description = "Accepted for parity with statement previews; no PDF appendix is generated by this tool." },
                includeReimbursedExpenses = new { type = "boolean", description = "Whether reimbursed expenses should be included." },
            }, ["contactId"]),
            McpToolSafetyLevel.ReadOnly,
            OutputSchema: ExpenseStatementPreviewOutputSchema()),
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

    private static object ContactGetOutputSchema() => new
    {
        type = "object",
        properties = new
        {
            found = new { type = "boolean" },
            contact = ContactDetailSchema(),
        },
    };

    private static object GigListOutputSchema() => new
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
            gigs = new
            {
                type = "array",
                items = GigSummarySchema(),
            },
            totalFees = McpSchema.Money("Total fees across returned gigs."),
            currency = new { type = "string" },
        },
    };

    private static object GigGetOutputSchema() => new
    {
        type = "object",
        properties = new
        {
            found = new { type = "boolean" },
            gig = GigDetailSchema(),
        },
    };

    private static object GigResourceListOutputSchema() => new
    {
        type = "object",
        properties = new
        {
            found = new { type = "boolean" },
            resources = new
            {
                type = "array",
                items = GigResourceSummarySchema(),
            },
        },
    };

    private static object GigSetlistGetOutputSchema() => new
    {
        type = "object",
        properties = new
        {
            found = new { type = "boolean" },
            hasActiveSetlist = new { type = "boolean" },
            setlist = GigSetlistDetailSchema(),
        },
    };

    private static object ExpenseStatementPreviewOutputSchema() => new
    {
        type = "object",
        properties = new
        {
            created = new { type = "boolean" },
            validationErrors = ValidationErrorsSchema(),
            statement = ExpenseStatementPreviewSchema(),
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

    private static object ContactDetailSchema() => new
    {
        type = "object",
        properties = new
        {
            contactId = UuidSchema(),
            name = new { type = "string" },
            email = new { type = "string" },
            billingAddress = AddressSchema(),
            mileageRate = McpSchema.Money("Mileage rate for this contact."),
            passengerMileageRate = McpSchema.Money("Passenger mileage rate for this contact."),
            invoiceFilenamePattern = new { type = "string" },
            invoiceEmailSubjectPattern = new { type = "string" },
            gigCount = new { type = "integer" },
            invoiceCount = new { type = "integer" },
        },
    };

    private static object AddressSchema() => new
    {
        type = "object",
        properties = new
        {
            line1 = new { type = "string" },
            line2 = new { type = "string" },
            city = new { type = "string" },
            stateOrCounty = new { type = "string" },
            postalCode = new { type = "string" },
            country = new { type = "string" },
        },
    };

    private static object GigSummarySchema() => new
    {
        type = "object",
        properties = new
        {
            gigId = UuidSchema(),
            title = new { type = "string" },
            date = DateSchema(),
            venue = new { type = "string" },
            contactId = UuidSchema(),
            contactName = new { type = "string" },
            status = McpSchema.Enum<GigStatus>("Gig lifecycle status."),
            gigType = McpSchema.Enum<GigType>("Nature of the musician work."),
            fee = McpSchema.Money("Gig fee."),
            isInvoiced = new { type = "boolean" },
            invoiceId = UuidSchema(),
            currency = new { type = "string" },
        },
    };

    private static object GigDetailSchema() => new
    {
        type = "object",
        properties = new
        {
            gigId = UuidSchema(),
            title = new { type = "string" },
            date = DateSchema(),
            venue = new { type = "string" },
            contactId = UuidSchema(),
            contactName = new { type = "string" },
            status = McpSchema.Enum<GigStatus>("Gig lifecycle status."),
            gigType = McpSchema.Enum<GigType>("Nature of the musician work."),
            fee = McpSchema.Money("Gig fee."),
            travelMiles = new { type = "number" },
            passengerCount = new { type = "integer" },
            wasDriving = new { type = "boolean" },
            notes = new { type = "string" },
            isInvoiced = new { type = "boolean" },
            invoice = InvoiceSummarySchema(),
            expenses = new { type = "array", items = GigExpenseSummarySchema() },
            resources = new { type = "array", items = GigResourceSummarySchema() },
            currency = new { type = "string" },
        },
    };

    private static object GigExpenseSummarySchema() => new
    {
        type = "object",
        properties = new
        {
            expenseId = UuidSchema(),
            description = new { type = "string" },
            amount = McpSchema.Money("Expense amount."),
            sortOrder = new { type = "integer" },
            reimbursementStatus = McpSchema.Enum<GigExpenseReimbursementStatus>("Expense reimbursement status."),
            attachmentCount = new { type = "integer" },
            attachmentFileNames = StringArraySchema(),
            currency = new { type = "string" },
        },
    };

    private static object GigResourceSummarySchema() => new
    {
        type = "object",
        properties = new
        {
            resourceId = UuidSchema(),
            resourceType = McpSchema.Enum<GigExternalResourceType>("Resource type."),
            purpose = McpSchema.Enum<GigExternalResourcePurpose>("Resource purpose."),
            title = new { type = "string" },
            url = new { type = "string" },
            notes = new { type = "string" },
            isPrimary = new { type = "boolean" },
            createdAt = DateTimeSchema(),
            updatedAt = DateTimeSchema(),
            attachments = new { type = "array", items = GigResourceAttachmentSchema() },
        },
    };

    private static object GigResourceAttachmentSchema() => new
    {
        type = "object",
        properties = new
        {
            attachmentId = UuidSchema(),
            fileName = new { type = "string" },
            contentType = new { type = "string" },
            sizeBytes = new { type = "integer" },
            createdAt = DateTimeSchema(),
        },
    };

    private static object GigSetlistDetailSchema() => new
    {
        type = "object",
        properties = new
        {
            importId = UuidSchema(),
            gigId = UuidSchema(),
            resourceId = UuidSchema(),
            spreadsheetId = new { type = "string" },
            worksheetId = new { type = "string" },
            worksheetName = new { type = "string" },
            sourceUrl = new { type = "string" },
            importedAtUtc = DateTimeSchema(),
            items = new { type = "array", items = GigSetlistItemSchema() },
        },
    };

    private static object GigSetlistItemSchema() => new
    {
        type = "object",
        properties = new
        {
            sortOrder = new { type = "integer" },
            sourceRowNumber = new { type = "integer" },
            kind = McpSchema.Enum<GigSetListItemKind>("Setlist row kind."),
            include = new { type = "boolean" },
            section = new { type = "string" },
            padNumber = new { type = "string" },
            key = new { type = "string" },
            title = new { type = "string" },
            notes = new { type = "string" },
            confidence = McpSchema.Enum<GigSetListItemConfidence>("Setlist item confidence."),
        },
    };

    private static object ExpenseStatementPreviewSchema() => new
    {
        type = "object",
        properties = new
        {
            contactId = UuidSchema(),
            contactName = new { type = "string" },
            statementDate = DateSchema(),
            gigs = new { type = "array", items = ExpenseStatementGigPreviewSchema() },
            total = McpSchema.Money("Expense statement total."),
            expenseCount = new { type = "integer" },
            receiptAttachmentCount = new { type = "integer" },
            currency = new { type = "string" },
        },
    };

    private static object ExpenseStatementGigPreviewSchema() => new
    {
        type = "object",
        properties = new
        {
            gigId = UuidSchema(),
            title = new { type = "string" },
            date = DateSchema(),
            venue = new { type = "string" },
            isInvoiced = new { type = "boolean" },
            expenses = new { type = "array", items = ExpenseStatementExpensePreviewSchema() },
            total = McpSchema.Money("Total expenses for this gig."),
            currency = new { type = "string" },
        },
    };

    private static object ExpenseStatementExpensePreviewSchema() => new
    {
        type = "object",
        properties = new
        {
            expenseId = UuidSchema(),
            description = new { type = "string" },
            amount = McpSchema.Money("Expense amount."),
            sortOrder = new { type = "integer" },
            attachments = new { type = "array", items = GigResourceAttachmentSchema() },
            currency = new { type = "string" },
        },
    };

    private static object ValidationErrorsSchema() => new
    {
        type = "object",
        additionalProperties = StringArraySchema(),
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
            gigType = McpSchema.Enum<GigType>("Nature of the proposed musician work."),
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
            ["gigType"] = McpSchema.Enum<GigType>("Nature of the proposed musician work."),
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
