using Glovelly.Api.Auth;
using Glovelly.Api.Data;
using Glovelly.Api.Models;
using Glovelly.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace Glovelly.Api.Endpoints;

public static class GigEndpoints
{
    public static RouteGroupBuilder MapGigEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/generate-invoice", async (
            GenerateInvoiceFromGigSelectionRequest request,
            AppDbContext db,
            ClaimsPrincipal user,
            ICurrentUserAccessor currentUserAccessor,
            IInvoiceWorkflowService invoiceWorkflowService) =>
        {
            var userId = currentUserAccessor.TryGetUserId(user);
            var gigIds = request.GigIds
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToList();

            if (gigIds.Count == 0)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["gigIds"] = ["Select at least one gig."]
                });
            }

            var gigs = await db.Gigs
                .WhereVisibleTo(userId)
                .Include(value => value.Client)
                .Include(value => value.Expenses)
                .Where(value => gigIds.Contains(value.Id))
                .OrderBy(value => value.Date)
                .ThenBy(value => value.Title)
                .ToListAsync();

            if (gigs.Count != gigIds.Count)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["gigIds"] = ["One or more selected gigs do not exist."]
                });
            }

            if (gigs.Any(gig => gig.InvoiceId.HasValue))
            {
                return Results.Conflict(new
                {
                    message = "All selected gigs must be uninvoiced before creating a combined invoice.",
                });
            }

            var distinctClientIds = gigs
                .Select(gig => gig.ClientId)
                .Distinct()
                .ToList();

            if (distinctClientIds.Count != 1)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["gigIds"] = ["Selected gigs must all belong to the same client."]
                });
            }

            var client = gigs[0].Client;
            if (client is null)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["clientId"] = ["Client does not exist."]
                });
            }

            var firstGig = gigs[0];
            var invoice = await invoiceWorkflowService.GenerateInvoiceForGigAsync(firstGig, client, userId);

            foreach (var gig in gigs.Skip(1))
            {
                gig.InvoiceId = invoice.Id;
                gig.InvoicedAt = DateTimeOffset.UtcNow;
                EndpointSupport.StampUpdate(gig, userId);
                await invoiceWorkflowService.SyncGeneratedInvoiceLinesForGigAsync(gig, userId);
            }

            await db.SaveChangesAsync();

            var refreshedInvoice = await db.Invoices
                .WhereVisibleTo(userId)
                .Include(value => value.Lines)
                .FirstAsync(value => value.Id == invoice.Id);

            await invoiceWorkflowService.RedraftInvoiceAsync(refreshedInvoice, client, userId);
            await db.SaveChangesAsync();

            return Results.Created($"/invoices/{refreshedInvoice.Id}", refreshedInvoice);
        });

        group.MapGet("/", async (AppDbContext db, ClaimsPrincipal user, ICurrentUserAccessor currentUserAccessor) =>
        {
            var userId = currentUserAccessor.TryGetUserId(user);
            var gigs = await db.Gigs
                .WhereVisibleTo(userId)
                .AsNoTracking()
                .Include(gig => gig.Expenses)
                    .ThenInclude(expense => expense.Attachments)
                .OrderBy(gig => gig.Date)
                .ThenBy(gig => gig.Title)
                .ToListAsync();

            return Results.Ok(gigs);
        });

        group.MapGet("/{id:guid}", async (Guid id, AppDbContext db, ClaimsPrincipal user, ICurrentUserAccessor currentUserAccessor) =>
        {
            var userId = currentUserAccessor.TryGetUserId(user);
            var gig = await db.Gigs
                .WhereVisibleTo(userId)
                .AsNoTracking()
                .Include(value => value.Expenses)
                    .ThenInclude(expense => expense.Attachments)
                .FirstOrDefaultAsync(gig => gig.Id == id);

            return gig is null ? Results.NotFound() : Results.Ok(gig);
        });

        group.MapPost("/{id:guid}/generate-invoice", async (
            Guid id,
            AppDbContext db,
            ClaimsPrincipal user,
            ICurrentUserAccessor currentUserAccessor,
            IInvoiceWorkflowService invoiceWorkflowService) =>
        {
            var userId = currentUserAccessor.TryGetUserId(user);
            var gig = await db.Gigs
                .WhereVisibleTo(userId)
                .Include(value => value.Client)
                .Include(value => value.Expenses)
                .FirstOrDefaultAsync(value => value.Id == id);

            if (gig is null)
            {
                return Results.NotFound();
            }

            if (gig.InvoiceId.HasValue)
            {
                return Results.Conflict(new
                {
                    message = "This gig has already been invoiced.",
                    invoiceId = gig.InvoiceId,
                });
            }

            var client = gig.Client;
            if (client is null)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["clientId"] = ["Client does not exist."]
                });
            }

            var invoice = await invoiceWorkflowService.GenerateInvoiceForGigAsync(gig, client, userId);
            await db.SaveChangesAsync();

            return Results.Created($"/invoices/{invoice.Id}", invoice);
        });

        group.MapPost("/", async (
            Gig gig,
            AppDbContext db,
            ClaimsPrincipal user,
            ICurrentUserAccessor currentUserAccessor,
            IInvoiceWorkflowService invoiceWorkflowService) =>
        {
            var userId = currentUserAccessor.TryGetUserId(user);
            var gigValidation = EndpointSupport.ValidateGigRequest(gig);
            if (gigValidation is not null)
            {
                return gigValidation;
            }

            if (!await db.Clients
                    .WhereVisibleTo(userId)
                    .AnyAsync(client => client.Id == gig.ClientId))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["clientId"] = ["Client does not exist."]
                });
            }

            if (gig.InvoiceId.HasValue)
            {
                var invoice = await db.Invoices
                    .WhereVisibleTo(userId)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(value => value.Id == gig.InvoiceId.Value);

                if (invoice is null)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["invoiceId"] = ["Invoice does not exist."]
                    });
                }

                if (invoice.ClientId != gig.ClientId)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["invoiceId"] = ["Invoice client must match the gig client."]
                    });
                }
            }

            gig.Id = Guid.NewGuid();
            gig.Title = gig.Title.Trim();
            gig.Venue = gig.Venue.Trim();
            gig.Notes = gig.Notes?.Trim();
            gig.Client = null;
            gig.Invoice = null;
            gig.Expenses = EndpointSupport.NormalizeGigExpenses(gig.Expenses);
            gig.InvoicedAt = EndpointSupport.ResolveInvoicedAt(gig.InvoiceId, null, null, gig.InvoicedAt);
            EndpointSupport.StampCreate(gig, userId);

            db.Gigs.Add(gig);
            await invoiceWorkflowService.SyncGeneratedInvoiceLinesForGigAsync(gig, userId);
            await db.SaveChangesAsync();

            return Results.Created($"/gigs/{gig.Id}", gig);
        });

        group.MapPut("/{id:guid}", async (
            Guid id,
            Gig request,
            AppDbContext db,
            ClaimsPrincipal user,
            ICurrentUserAccessor currentUserAccessor,
            IExpenseAttachmentStore attachmentStore,
            IInvoiceWorkflowService invoiceWorkflowService) =>
        {
            var userId = currentUserAccessor.TryGetUserId(user);
            var gig = await db.Gigs
                .WhereVisibleTo(userId)
                .Include(value => value.Expenses)
                    .ThenInclude(expense => expense.Attachments)
                .FirstOrDefaultAsync(value => value.Id == id);

            if (gig is null)
            {
                return Results.NotFound();
            }

            var gigValidation = EndpointSupport.ValidateGigRequest(request);
            if (gigValidation is not null)
            {
                return gigValidation;
            }

            if (!await db.Clients
                    .WhereVisibleTo(userId)
                    .AnyAsync(client => client.Id == request.ClientId))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["clientId"] = ["Client does not exist."]
                });
            }

            if (request.InvoiceId.HasValue)
            {
                var invoice = await db.Invoices
                    .WhereVisibleTo(userId)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(value => value.Id == request.InvoiceId.Value);

                if (invoice is null)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["invoiceId"] = ["Invoice does not exist."]
                    });
                }

                if (invoice.ClientId != request.ClientId)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["invoiceId"] = ["Invoice client must match the gig client."]
                    });
                }
            }

            var normalizedExpenses = EndpointSupport.NormalizeGigExpenses(request.Expenses, preserveIds: false);
            var previousInvoiceId = gig.InvoiceId;
            var existingExpenses = gig.Expenses
                .OrderBy(expense => expense.SortOrder)
                .ThenBy(expense => expense.Description)
                .ToList();

            gig.ClientId = request.ClientId;
            gig.InvoiceId = request.InvoiceId;
            gig.Title = request.Title.Trim();
            gig.Date = request.Date;
            gig.Venue = request.Venue.Trim();
            gig.Fee = request.Fee;
            gig.TravelMiles = request.TravelMiles;
            gig.PassengerCount = request.PassengerCount;
            gig.Notes = request.Notes?.Trim();
            gig.WasDriving = request.WasDriving;
            gig.Status = request.Status;
            gig.InvoicedAt = EndpointSupport.ResolveInvoicedAt(
                request.InvoiceId,
                previousInvoiceId,
                gig.InvoicedAt,
                request.InvoicedAt);

            EndpointSupport.StampUpdate(gig, userId);
            await db.SaveChangesAsync();

            var sharedExpenseCount = Math.Min(existingExpenses.Count, normalizedExpenses.Count);
            for (var i = 0; i < sharedExpenseCount; i++)
            {
                existingExpenses[i].SortOrder = normalizedExpenses[i].SortOrder;
                existingExpenses[i].Description = normalizedExpenses[i].Description;
                existingExpenses[i].Amount = normalizedExpenses[i].Amount;
            }

            if (existingExpenses.Count > normalizedExpenses.Count)
            {
                var expensesToRemove = existingExpenses.Skip(normalizedExpenses.Count).ToList();
                foreach (var attachment in expensesToRemove.SelectMany(expense => expense.Attachments).ToList())
                {
                    await attachmentStore.DeleteAsync(attachment.StorageKey);
                }

                db.GigExpenses.RemoveRange(expensesToRemove);
            }

            foreach (var expense in normalizedExpenses.Skip(sharedExpenseCount))
            {
                expense.GigId = gig.Id;
                db.GigExpenses.Add(expense);
            }

            await db.SaveChangesAsync();

            gig = await db.Gigs
                .Include(value => value.Expenses)
                    .ThenInclude(expense => expense.Attachments)
                .FirstAsync(value => value.Id == id);

            await invoiceWorkflowService.SyncGeneratedInvoiceLinesForGigAsync(gig, userId);
            await db.SaveChangesAsync();

            return Results.Ok(gig);
        });

        group.MapDelete("/{id:guid}", async (
            Guid id,
            AppDbContext db,
            ClaimsPrincipal user,
            ICurrentUserAccessor currentUserAccessor,
            IExpenseAttachmentStore attachmentStore,
            IInvoiceWorkflowService invoiceWorkflowService) =>
        {
            var userId = currentUserAccessor.TryGetUserId(user);
            var gig = await db.Gigs
                .WhereVisibleTo(userId)
                .Include(value => value.Expenses)
                    .ThenInclude(expense => expense.Attachments)
                .FirstOrDefaultAsync(gig => gig.Id == id);
            if (gig is null)
            {
                return Results.NotFound();
            }

            foreach (var attachment in gig.Expenses.SelectMany(expense => expense.Attachments).ToList())
            {
                await attachmentStore.DeleteAsync(attachment.StorageKey);
            }

            _ = await invoiceWorkflowService.RemoveSystemGeneratedInvoiceLinesForGigAsync(gig.Id);
            db.Gigs.Remove(gig);
            await db.SaveChangesAsync();

            return Results.NoContent();
        });

        group.MapGet("/{gigId:guid}/expenses/{expenseId:guid}/attachments", async (
            Guid gigId,
            Guid expenseId,
            AppDbContext db,
            ClaimsPrincipal user,
            ICurrentUserAccessor currentUserAccessor) =>
        {
            var userId = currentUserAccessor.TryGetUserId(user);
            var expense = await FindVisibleExpenseAsync(db, userId, gigId, expenseId, asNoTracking: true);

            return expense is null
                ? Results.NotFound()
                : Results.Ok(expense.Attachments.OrderBy(attachment => attachment.CreatedAt));
        });

        group.MapPost("/{gigId:guid}/expenses/{expenseId:guid}/attachments", async (
            Guid gigId,
            Guid expenseId,
            HttpRequest request,
            AppDbContext db,
            ClaimsPrincipal user,
            ICurrentUserAccessor currentUserAccessor,
            IExpenseAttachmentStore attachmentStore,
            IOptions<ExpenseAttachmentSettings> attachmentOptions) =>
        {
            if (!request.HasFormContentType)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["file"] = ["Upload a receipt file."]
                });
            }

            var userId = currentUserAccessor.TryGetUserId(user);
            var expense = await FindVisibleExpenseAsync(db, userId, gigId, expenseId, asNoTracking: false);
            if (expense is null)
            {
                return Results.NotFound();
            }

            var form = await request.ReadFormAsync();
            var file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
            var validation = ValidateAttachmentFile(file, attachmentOptions.Value);
            if (validation is not null)
            {
                return validation;
            }

            var attachmentId = Guid.NewGuid();
            var storageKey = BuildAttachmentStorageKey(userId, gigId, expenseId, attachmentId);
            await using var stream = file!.OpenReadStream();
            await attachmentStore.SaveAsync(storageKey, stream, file.ContentType);

            var displayFileName = Path.GetFileName(file.FileName);
            if (string.IsNullOrWhiteSpace(displayFileName))
            {
                displayFileName = "receipt";
            }

            var attachment = new ExpenseAttachment
            {
                Id = attachmentId,
                GigExpenseId = expenseId,
                FileName = displayFileName,
                ContentType = file.ContentType,
                SizeBytes = file.Length,
                StorageKey = storageKey,
                CreatedAt = DateTimeOffset.UtcNow,
            };

            db.ExpenseAttachments.Add(attachment);
            await db.SaveChangesAsync();

            return Results.Created($"/gigs/{gigId}/expenses/{expenseId}/attachments/{attachment.Id}", attachment);
        });

        group.MapGet("/{gigId:guid}/expenses/{expenseId:guid}/attachments/{attachmentId:guid}", async (
            Guid gigId,
            Guid expenseId,
            Guid attachmentId,
            AppDbContext db,
            ClaimsPrincipal user,
            ICurrentUserAccessor currentUserAccessor,
            IExpenseAttachmentStore attachmentStore) =>
        {
            var userId = currentUserAccessor.TryGetUserId(user);
            var attachment = await FindVisibleAttachmentAsync(db, userId, gigId, expenseId, attachmentId, asNoTracking: true);
            if (attachment is null)
            {
                return Results.NotFound();
            }

            try
            {
                var content = await attachmentStore.OpenReadAsync(attachment.StorageKey);
                return Results.File(
                    content.Content,
                    content.ContentType ?? attachment.ContentType,
                    attachment.FileName,
                    enableRangeProcessing: true);
            }
            catch (FileNotFoundException)
            {
                return Results.NotFound();
            }
        });

        group.MapDelete("/{gigId:guid}/expenses/{expenseId:guid}/attachments/{attachmentId:guid}", async (
            Guid gigId,
            Guid expenseId,
            Guid attachmentId,
            AppDbContext db,
            ClaimsPrincipal user,
            ICurrentUserAccessor currentUserAccessor,
            IExpenseAttachmentStore attachmentStore) =>
        {
            var userId = currentUserAccessor.TryGetUserId(user);
            var attachment = await FindVisibleAttachmentAsync(db, userId, gigId, expenseId, attachmentId, asNoTracking: false);
            if (attachment is null)
            {
                return Results.NotFound();
            }

            await attachmentStore.DeleteAsync(attachment.StorageKey);
            db.ExpenseAttachments.Remove(attachment);
            await db.SaveChangesAsync();

            return Results.NoContent();
        });

        return group;
    }

    private static Task<GigExpense?> FindVisibleExpenseAsync(
        AppDbContext db,
        Guid? userId,
        Guid gigId,
        Guid expenseId,
        bool asNoTracking)
    {
        var query = db.GigExpenses
            .Include(expense => expense.Attachments)
            .Include(expense => expense.Gig)
            .Where(expense => expense.Id == expenseId && expense.GigId == gigId)
            .Where(expense => expense.Gig != null
                && (expense.Gig.CreatedByUserId == null || expense.Gig.CreatedByUserId == userId));

        if (asNoTracking)
        {
            query = query.AsNoTracking();
        }

        return query.FirstOrDefaultAsync();
    }

    private static Task<ExpenseAttachment?> FindVisibleAttachmentAsync(
        AppDbContext db,
        Guid? userId,
        Guid gigId,
        Guid expenseId,
        Guid attachmentId,
        bool asNoTracking)
    {
        var query = db.ExpenseAttachments
            .Include(attachment => attachment.Expense)
                .ThenInclude(expense => expense!.Gig)
            .Where(attachment => attachment.Id == attachmentId
                && attachment.GigExpenseId == expenseId
                && attachment.Expense != null
                && attachment.Expense.GigId == gigId
                && attachment.Expense.Gig != null
                && (attachment.Expense.Gig.CreatedByUserId == null
                    || attachment.Expense.Gig.CreatedByUserId == userId));

        if (asNoTracking)
        {
            query = query.AsNoTracking();
        }

        return query.FirstOrDefaultAsync();
    }

    private static IResult? ValidateAttachmentFile(IFormFile? file, ExpenseAttachmentSettings settings)
    {
        if (file is null || file.Length == 0)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["file"] = ["Upload a receipt file."]
            });
        }

        if (file.Length > settings.MaxFileSizeBytes)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["file"] = [$"Receipt files must be {settings.MaxFileSizeBytes / 1024 / 1024} MB or smaller."]
            });
        }

        if (!settings.AllowedContentTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["file"] = ["Receipt files must be PDF, JPG, PNG, WebP or HEIC."]
            });
        }

        return null;
    }

    private static string BuildAttachmentStorageKey(Guid? userId, Guid gigId, Guid expenseId, Guid attachmentId)
    {
        var owner = userId?.ToString("N") ?? "system";
        return $"users/{owner}/gigs/{gigId:N}/expenses/{expenseId:N}/attachments/{attachmentId:N}";
    }

    private sealed record GenerateInvoiceFromGigSelectionRequest(IReadOnlyList<Guid> GigIds);
}
