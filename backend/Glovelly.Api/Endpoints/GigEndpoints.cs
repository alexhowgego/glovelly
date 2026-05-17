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

        group.MapPost("/receipt-drafts", async (
            HttpRequest request,
            AppDbContext db,
            ClaimsPrincipal user,
            ICurrentUserAccessor currentUserAccessor,
            IExpenseAttachmentStore attachmentStore,
            IOptions<ExpenseAttachmentSettings> attachmentOptions,
            IOptions<QuickReceiptCaptureSettings> quickReceiptOptions,
            TimeProvider timeProvider) =>
        {
            if (!request.HasFormContentType)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["file"] = ["Upload a receipt file."]
                });
            }

            var userId = currentUserAccessor.TryGetUserId(user);
            var form = await request.ReadFormAsync();
            var file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
            var validation = ValidateAttachmentFile(file, attachmentOptions.Value);
            if (validation is not null)
            {
                return validation;
            }

            var gigId = TryReadGigId(form);
            var today = DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
            var settings = NormalizeQuickReceiptSettings(quickReceiptOptions.Value);
            var candidates = await FindReceiptCandidatesAsync(
                db,
                userId,
                today,
                settings.CandidateCount,
                settings.AutoAttachWindowDays);
            Gig? gig;
            if (gigId.HasValue)
            {
                gig = await db.Gigs
                    .WhereVisibleTo(userId)
                    .Include(value => value.Expenses)
                    .FirstOrDefaultAsync(value => value.Id == gigId.Value);

                if (gig is null)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["gigId"] = ["Gig does not exist."]
                    });
                }
            }
            else
            {
                var nearestCandidate = candidates.FirstOrDefault();
                if (nearestCandidate is null)
                {
                    return Results.Conflict(new
                    {
                        message = $"No gig was within {settings.AutoAttachWindowDays} days. Choose a gig before saving this receipt draft.",
                        candidates = candidates.Select(candidate => ToReceiptGigCandidate(candidate, nearestCandidate?.Id)),
                        autoAttachWindowDays = settings.AutoAttachWindowDays,
                    });
                }

                gig = await db.Gigs
                    .WhereVisibleTo(userId)
                    .Include(value => value.Expenses)
                    .FirstAsync(value => value.Id == nearestCandidate.Id);
            }

            var expense = new GigExpense
            {
                Id = Guid.NewGuid(),
                GigId = gig.Id,
                SortOrder = gig.Expenses.Count == 0 ? 1 : gig.Expenses.Max(value => value.SortOrder) + 1,
                Description = "Receipt draft",
                Amount = 0m,
            };

            var attachmentId = Guid.NewGuid();
            var storageKey = BuildAttachmentStorageKey(userId, gig.Id, expense.Id, attachmentId);
            await using var stream = file!.OpenReadStream();
            await attachmentStore.SaveAsync(storageKey, stream, file.ContentType);

            var displayFileName = Path.GetFileName(file.FileName);
            if (string.IsNullOrWhiteSpace(displayFileName))
            {
                displayFileName = "receipt";
            }

            expense.Attachments.Add(new ExpenseAttachment
            {
                Id = attachmentId,
                GigExpenseId = expense.Id,
                FileName = displayFileName,
                ContentType = file.ContentType,
                SizeBytes = file.Length,
                StorageKey = storageKey,
                CreatedAt = DateTimeOffset.UtcNow,
            });

            db.GigExpenses.Add(expense);
            EndpointSupport.StampUpdate(gig, userId);
            await db.SaveChangesAsync();

            var savedGig = await db.Gigs
                .WhereVisibleTo(userId)
                .Include(value => value.Expenses)
                    .ThenInclude(value => value.Attachments)
                .FirstAsync(value => value.Id == gig.Id);

            return Results.Created($"/gigs/{savedGig.Id}", new
            {
                gig = savedGig,
                expenseId = expense.Id,
                attachmentId,
                inferredGig = !gigId.HasValue,
                candidates = candidates.Select(candidate => ToReceiptGigCandidate(candidate, gig.Id)),
                autoAttachWindowDays = settings.AutoAttachWindowDays,
                hasNearbyCandidates = candidates.Any(candidate =>
                    candidate.Id != gig.Id &&
                    candidate.DaysFromToday <= settings.AmbiguityWindowDays),
            });
        });

        group.MapPatch("/receipt-drafts/{expenseId:guid}", async (
            Guid expenseId,
            QuickReceiptDraftUpdateRequest update,
            AppDbContext db,
            ClaimsPrincipal user,
            ICurrentUserAccessor currentUserAccessor,
            IInvoiceWorkflowService invoiceWorkflowService) =>
        {
            var userId = currentUserAccessor.TryGetUserId(user);
            var targetGig = await db.Gigs
                .WhereVisibleTo(userId)
                .Include(value => value.Expenses)
                .FirstOrDefaultAsync(value => value.Id == update.GigId);

            if (targetGig is null)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["gigId"] = ["Gig does not exist."]
                });
            }

            var expense = await db.GigExpenses
                .Include(value => value.Attachments)
                .Include(value => value.Gig)
                .Where(value => value.Id == expenseId)
                .Where(value => value.Gig != null
                    && (value.Gig.CreatedByUserId == null || value.Gig.CreatedByUserId == userId))
                .FirstOrDefaultAsync();

            if (expense is null)
            {
                return Results.NotFound();
            }

            var description = update.Description?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(description))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["description"] = ["Expense description is required."]
                });
            }

            if (update.Amount < 0)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["amount"] = ["Expense amount cannot be negative."]
                });
            }

            var previousGigId = expense.GigId;
            var moved = previousGigId != targetGig.Id;

            expense.Description = description;
            expense.Amount = update.Amount;
            if (moved)
            {
                expense.GigId = targetGig.Id;
                expense.Gig = targetGig;
                expense.SortOrder = targetGig.Expenses.Count == 0
                    ? 1
                    : targetGig.Expenses.Max(value => value.SortOrder) + 1;
                targetGig.Expenses.Add(expense);
            }

            if (expense.Gig is not null)
            {
                EndpointSupport.StampUpdate(expense.Gig, userId);
            }

            EndpointSupport.StampUpdate(targetGig, userId);
            await db.SaveChangesAsync();

            var affectedGigIds = moved
                ? new[] { previousGigId, targetGig.Id }
                : new[] { targetGig.Id };

            var affectedGigs = await db.Gigs
                .WhereVisibleTo(userId)
                .Include(value => value.Expenses)
                .Where(value => affectedGigIds.Contains(value.Id))
                .ToListAsync();

            foreach (var affectedGig in affectedGigs)
            {
                await invoiceWorkflowService.SyncGeneratedInvoiceLinesForGigAsync(affectedGig, userId);
            }

            await db.SaveChangesAsync();

            var savedGigs = await db.Gigs
                .WhereVisibleTo(userId)
                .AsNoTracking()
                .Include(value => value.Expenses)
                    .ThenInclude(value => value.Attachments)
                .Where(value => affectedGigIds.Contains(value.Id))
                .ToListAsync();

            var savedTargetGig = savedGigs.First(value => value.Id == targetGig.Id);
            var previousGig = moved
                ? savedGigs.FirstOrDefault(value => value.Id == previousGigId)
                : null;

            return Results.Ok(new
            {
                gig = savedTargetGig,
                previousGig,
                expenseId,
                moved,
            });
        });

        group.MapPatch("/{gigId:guid}/expenses/reimbursement", async (
            Guid gigId,
            GigExpenseReimbursementUpdateRequest request,
            AppDbContext db,
            ClaimsPrincipal user,
            ICurrentUserAccessor currentUserAccessor) =>
        {
            var userId = currentUserAccessor.TryGetUserId(user);
            var expenseIds = (request.ExpenseIds ?? [])
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToList();

            if (expenseIds.Count == 0)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["expenseIds"] = ["Select at least one expense."]
                });
            }

            if (!Enum.IsDefined(request.Status))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["status"] = ["Reimbursement status is invalid."]
                });
            }

            var method = request.Method?.Trim();
            var note = request.Note?.Trim();
            if (request.Status is GigExpenseReimbursementStatus.Reimbursed)
            {
                if (!request.ReimbursedAt.HasValue)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["reimbursedAt"] = ["Reimbursed date is required."]
                    });
                }

                if (string.IsNullOrWhiteSpace(method) && string.IsNullOrWhiteSpace(note))
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["note"] = ["Record a reimbursement method or note."]
                    });
                }
            }

            var gig = await db.Gigs
                .WhereVisibleTo(userId)
                .Include(value => value.Expenses)
                .FirstOrDefaultAsync(value => value.Id == gigId);

            if (gig is null)
            {
                return Results.NotFound();
            }

            if (request.LinkedInvoiceId.HasValue)
            {
                var invoice = await db.Invoices
                    .WhereVisibleTo(userId)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(value => value.Id == request.LinkedInvoiceId.Value);

                if (invoice is null)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["linkedInvoiceId"] = ["Linked invoice does not exist."]
                    });
                }

                if (invoice.ClientId != gig.ClientId)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["linkedInvoiceId"] = ["Linked invoice client must match the gig client."]
                    });
                }
            }

            var expenses = gig.Expenses
                .Where(expense => expenseIds.Contains(expense.Id))
                .ToList();

            if (expenses.Count != expenseIds.Count)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["expenseIds"] = ["One or more expenses do not belong to this gig."]
                });
            }

            foreach (var expense in expenses)
            {
                ApplyReimbursementUpdate(expense, request, method, note, userId);
            }

            EndpointSupport.StampUpdate(gig, userId);
            await db.SaveChangesAsync();

            var savedGig = await db.Gigs
                .WhereVisibleTo(userId)
                .AsNoTracking()
                .Include(value => value.Expenses)
                    .ThenInclude(expense => expense.Attachments)
                .FirstAsync(value => value.Id == gigId);

            return Results.Ok(savedGig);
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

            var requestedInvoiceId = request.InvoiceId ?? gig.InvoiceId;

            if (requestedInvoiceId.HasValue)
            {
                var invoice = await db.Invoices
                    .WhereVisibleTo(userId)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(value => value.Id == requestedInvoiceId.Value);

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
            gig.InvoiceId = requestedInvoiceId;
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
                requestedInvoiceId,
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

    private static Guid? TryReadGigId(IFormCollection form)
    {
        var rawValue = form["gigId"].FirstOrDefault();
        return Guid.TryParse(rawValue, out var gigId) && gigId != Guid.Empty ? gigId : null;
    }

    private static async Task<List<ReceiptGigCandidate>> FindReceiptCandidatesAsync(
        AppDbContext db,
        Guid? userId,
        DateOnly today,
        int candidateCount,
        int autoAttachWindowDays)
    {
        var gigs = await db.Gigs
            .WhereVisibleTo(userId)
            .AsNoTracking()
            .Where(value => value.Status != GigStatus.Cancelled)
            .ToListAsync();

        return gigs
            .Select(gig => new ReceiptGigCandidate(
                gig.Id,
                gig.ClientId,
                gig.Title,
                gig.Date,
                gig.Venue,
                gig.Status,
                Math.Abs(gig.Date.DayNumber - today.DayNumber)))
            .Where(candidate => candidate.DaysFromToday <= autoAttachWindowDays)
            .OrderBy(candidate => candidate.DaysFromToday)
            .ThenBy(candidate => candidate.Date)
            .ThenBy(candidate => candidate.Title)
            .Take(candidateCount)
            .ToList();
    }

    private static QuickReceiptCaptureSettings NormalizeQuickReceiptSettings(QuickReceiptCaptureSettings settings)
    {
        return new QuickReceiptCaptureSettings
        {
            CandidateCount = Math.Clamp(settings.CandidateCount, 1, 20),
            AutoAttachWindowDays = Math.Clamp(settings.AutoAttachWindowDays, 0, 365),
            AmbiguityWindowDays = Math.Clamp(settings.AmbiguityWindowDays, 0, 365),
        };
    }

    private static object ToReceiptGigCandidate(ReceiptGigCandidate candidate, Guid? selectedGigId)
    {
        return new
        {
            candidate.Id,
            candidate.ClientId,
            candidate.Title,
            candidate.Date,
            candidate.Venue,
            candidate.Status,
            candidate.DaysFromToday,
            IsSelected = candidate.Id == selectedGigId,
        };
    }

    private static void ApplyReimbursementUpdate(
        GigExpense expense,
        GigExpenseReimbursementUpdateRequest request,
        string? method,
        string? note,
        Guid? userId)
    {
        expense.ReimbursementStatus = request.Status;
        expense.ReimbursementUpdatedAt = DateTimeOffset.UtcNow;
        expense.ReimbursementUpdatedByUserId = userId;

        if (request.Status is GigExpenseReimbursementStatus.Unreimbursed)
        {
            expense.ReimbursedAt = null;
            expense.ReimbursementMethod = null;
            expense.ReimbursementNote = null;
            expense.ReimbursementInvoiceId = null;
            return;
        }

        expense.ReimbursedAt = request.Status is GigExpenseReimbursementStatus.Reimbursed
            ? request.ReimbursedAt
            : null;
        expense.ReimbursementMethod = string.IsNullOrWhiteSpace(method) ? null : method;
        expense.ReimbursementNote = string.IsNullOrWhiteSpace(note) ? null : note;
        expense.ReimbursementInvoiceId = request.LinkedInvoiceId;
    }

    private sealed record GenerateInvoiceFromGigSelectionRequest(IReadOnlyList<Guid> GigIds);

    // Reimbursement metadata tracks external recovery of an expense. It is not an invoice
    // adjustment: adjustments change invoice totals, while this state controls whether
    // an existing gig expense should be claimable by default in future generated documents.
    private sealed record GigExpenseReimbursementUpdateRequest(
        IReadOnlyList<Guid>? ExpenseIds,
        GigExpenseReimbursementStatus Status,
        DateTimeOffset? ReimbursedAt,
        string? Method,
        string? Note,
        Guid? LinkedInvoiceId);

    private sealed record QuickReceiptDraftUpdateRequest(Guid GigId, string Description, decimal Amount);

    private sealed record ReceiptGigCandidate(
        Guid Id,
        Guid ClientId,
        string Title,
        DateOnly Date,
        string Venue,
        GigStatus Status,
        int DaysFromToday);
}
