using System.Diagnostics;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using Glovelly.Api.Auth;
using Glovelly.Api.Data;
using Glovelly.Api.Models;
using Glovelly.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace Glovelly.Api.Endpoints;

internal static class GigSetListImportEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public static RouteGroupBuilder MapGigSetListImportEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/{gigId:guid}/setlist-imports", async (
            Guid gigId,
            AppDbContext db,
            ClaimsPrincipal user,
            ICurrentUserAccessor currentUserAccessor) =>
        {
            var userId = currentUserAccessor.TryGetUserId(user);
            if (!await db.Gigs.WhereVisibleTo(userId).AnyAsync(gig => gig.Id == gigId))
            {
                return Results.NotFound();
            }

            var imports = await db.GigSetListImports
                .AsNoTracking()
                .Include(value => value.Items)
                .Where(value => value.GigId == gigId)
                .OrderByDescending(value => value.ImportedAtUtc)
                .Select(value => ToImportResponse(value))
                .ToListAsync();

            return Results.Ok(imports);
        });

        group.MapGet("/{gigId:guid}/setlist-imports/active", async (
            Guid gigId,
            AppDbContext db,
            ClaimsPrincipal user,
            ICurrentUserAccessor currentUserAccessor,
            CancellationToken cancellationToken) =>
        {
            var userId = currentUserAccessor.TryGetUserId(user);
            if (!await db.Gigs.WhereVisibleTo(userId).AnyAsync(gig => gig.Id == gigId, cancellationToken))
            {
                return Results.NotFound();
            }

            var activeImport = await db.GigSetListImports
                .AsNoTracking()
                .Include(value => value.Items)
                .Where(value => value.GigId == gigId && value.IsActive)
                .OrderByDescending(value => value.ImportedAtUtc)
                .FirstOrDefaultAsync(cancellationToken);

            return activeImport is null ? Results.NotFound() : Results.Ok(ToImportResponse(activeImport));
        });

        group.MapGet("/{gigId:guid}/setlist-imports/active/forscore-export", async (
            Guid gigId,
            AppDbContext db,
            ClaimsPrincipal user,
            ICurrentUserAccessor currentUserAccessor,
            IForScoreSetListExportService exportService,
            CancellationToken cancellationToken) =>
        {
            var userId = currentUserAccessor.TryGetUserId(user);
            var gig = await db.Gigs
                .AsNoTracking()
                .WhereVisibleTo(userId)
                .FirstOrDefaultAsync(value => value.Id == gigId, cancellationToken);
            if (gig is null)
            {
                return Results.NotFound();
            }

            var activeImport = await db.GigSetListImports
                .AsNoTracking()
                .Include(value => value.Items)
                .Where(value => value.GigId == gigId && value.IsActive)
                .OrderByDescending(value => value.ImportedAtUtc)
                .FirstOrDefaultAsync(cancellationToken);
            if (activeImport is null)
            {
                return Results.NotFound(new { message = "No active set list is available for this gig." });
            }

            var includedSongs = activeImport.Items
                .Where(item => item.Kind == GigSetListItemKind.Song && item.Include)
                .OrderBy(item => item.SortOrder)
                .ToList();
            if (includedSongs.Count == 0)
            {
                return EndpointSupport.ValidationProblem("items", "Include at least one song row before exporting to forScore.");
            }

            var unmappedRows = includedSongs
                .Where(item => !item.ForScoreChartId.HasValue || string.IsNullOrWhiteSpace(item.ForScoreChartFilePath))
                .Select(item => new UnexportableSetListItemResponse(item.Id, item.SourceRowNumber, item.Title))
                .ToList();
            if (unmappedRows.Count > 0)
            {
                return Results.Conflict(new
                {
                    message = "Select forScore charts for all included song rows before exporting.",
                    missingItems = unmappedRows,
                });
            }

            var exportItems = includedSongs
                .Select(item => new ForScoreSetListExportItem(
                    string.IsNullOrWhiteSpace(item.ForScoreChartTitle) ? item.Title : item.ForScoreChartTitle,
                    item.ForScoreChartFilePath!))
                .ToList();
            var content = exportService.BuildExport(gig.Title, exportItems);
            var fileName = exportService.BuildFileName(gig.Title);
            return Results.File(content, "application/xml; charset=utf-8", fileName);
        });

        group.MapGet("/{gigId:guid}/setlist-imports/source", async (
            Guid gigId,
            Guid? resourceId,
            AppDbContext db,
            ClaimsPrincipal user,
            ICurrentUserAccessor currentUserAccessor,
            IGoogleConnectionService googleConnectionService,
            IGoogleSheetsApiClient sheetsApiClient,
            CancellationToken cancellationToken) =>
        {
            var userId = currentUserAccessor.TryGetUserId(user);
            var sourceResult = await ResolveSourceAsync(db, userId, gigId, resourceId, cancellationToken);
            if (sourceResult.Result is not null)
            {
                return sourceResult.Result;
            }

            var connection = await ResolveSheetsConnectionAsync(googleConnectionService, userId, cancellationToken);
            if (connection is null)
            {
                return Results.Problem(
                    title: "Google Sheets is not connected",
                    detail: "Reconnect Google to grant Sheets read access.",
                    statusCode: StatusCodes.Status409Conflict);
            }

            var accessTokenResult = await ResolveSheetsAccessTokenAsync(
                googleConnectionService,
                connection,
                cancellationToken);
            if (accessTokenResult.Result is not null)
            {
                return accessTokenResult.Result;
            }

            var metadataResult = await ReadSpreadsheetMetadataAsync(
                sheetsApiClient,
                accessTokenResult.AccessToken!,
                sourceResult.SpreadsheetId!,
                cancellationToken);
            if (metadataResult.Result is not null)
            {
                return metadataResult.Result;
            }

            return Results.Ok(new SetListSourceResponse(
                sourceResult.Resource!.Id,
                sourceResult.Resource.Title,
                sourceResult.Resource.Url!,
                metadataResult.Metadata!.SpreadsheetId,
                metadataResult.Metadata.Sheets));
        });

        group.MapPost("/{gigId:guid}/setlist-imports/preview", async (
            Guid gigId,
            SetListPreviewRequest request,
            AppDbContext db,
            ClaimsPrincipal user,
            ICurrentUserAccessor currentUserAccessor,
            IGoogleConnectionService googleConnectionService,
            IGoogleSheetsApiClient sheetsApiClient,
            ISetListSheetParser parser,
            CancellationToken cancellationToken) =>
        {
            var userId = currentUserAccessor.TryGetUserId(user);
            var sourceResult = await ResolveSourceAsync(db, userId, gigId, request.ResourceId, cancellationToken);
            if (sourceResult.Result is not null)
            {
                return sourceResult.Result;
            }

            var connection = await ResolveSheetsConnectionAsync(googleConnectionService, userId, cancellationToken);
            if (connection is null)
            {
                return Results.Problem(
                    title: "Google Sheets is not connected",
                    detail: "Reconnect Google to grant Sheets read access.",
                    statusCode: StatusCodes.Status409Conflict);
            }

            var accessTokenResult = await ResolveSheetsAccessTokenAsync(
                googleConnectionService,
                connection,
                cancellationToken);
            if (accessTokenResult.Result is not null)
            {
                return accessTokenResult.Result;
            }

            var worksheetNameResult = await ResolveWorksheetNameAsync(
                sheetsApiClient,
                accessTokenResult.AccessToken!,
                sourceResult.SpreadsheetId!,
                request.WorksheetName,
                request.WorksheetId,
                cancellationToken);
            if (worksheetNameResult.Result is not null)
            {
                return worksheetNameResult.Result;
            }

            var valuesResult = await ReadWorksheetValuesAsync(
                sheetsApiClient,
                accessTokenResult.AccessToken!,
                sourceResult.SpreadsheetId!,
                worksheetNameResult.WorksheetName!,
                cancellationToken);
            if (valuesResult.Result is not null)
            {
                return valuesResult.Result;
            }

            var items = parser.Parse(valuesResult.Values!.Rows);

            return Results.Ok(new SetListPreviewResponse(
                sourceResult.Resource!.Id,
                sourceResult.Resource.Title,
                sourceResult.Resource.Url!,
                sourceResult.SpreadsheetId!,
                request.WorksheetId,
                worksheetNameResult.WorksheetName!,
                items));
        });

        group.MapPost("/{gigId:guid}/setlist-imports/chart-matches/preview", async (
            Guid gigId,
            SetListDraftChartMatchPreviewRequest request,
            AppDbContext db,
            ClaimsPrincipal user,
            ICurrentUserAccessor currentUserAccessor,
            ISetListChartMatcher chartMatcher,
            HttpContext httpContext,
            ILoggerFactory loggerFactory,
            CancellationToken cancellationToken) =>
        {
            var logger = loggerFactory.CreateLogger("Glovelly.Api.Endpoints.GigSetListImportEndpoints");
            var requestId = httpContext.Request.Headers.TryGetValue("X-Glovelly-Request-Id", out var requestIdHeader) && !string.IsNullOrWhiteSpace(requestIdHeader)
                ? requestIdHeader.ToString()
                : httpContext.TraceIdentifier;
            var stopwatch = Stopwatch.StartNew();
            var includedSongCount = request.Items.Count(item => item.Kind == GigSetListItemKind.Song && item.Include);
            logger.LogInformation(
                "Set list chart match preview started: RequestId {RequestId}, GigId {GigId}, UseAi {UseAi}, ItemCount {ItemCount}, IncludedSongCount {IncludedSongCount}, ContentLength {ContentLength}, UserAgent {UserAgent}.",
                requestId,
                gigId,
                request.UseAi,
                request.Items.Count,
                includedSongCount,
                httpContext.Request.ContentLength,
                httpContext.Request.Headers.UserAgent.ToString());

            var userId = currentUserAccessor.TryGetUserId(user);
            if (!await db.Gigs.WhereVisibleTo(userId).AnyAsync(gig => gig.Id == gigId, cancellationToken))
            {
                logger.LogInformation(
                    "Set list chart match preview returned not found: RequestId {RequestId}, GigId {GigId}, ElapsedMilliseconds {ElapsedMilliseconds}.",
                    requestId,
                    gigId,
                    stopwatch.ElapsedMilliseconds);
                return Results.NotFound();
            }

            try
            {
                var matches = await chartMatcher.MatchAsync(userId, request.Items.Select(ToMatchInput).ToList(), cancellationToken, request.UseAi);
                logger.LogInformation(
                    "Set list chart match preview completed: RequestId {RequestId}, GigId {GigId}, UseAi {UseAi}, ResultCount {ResultCount}, SuggestedCount {SuggestedCount}, NeedsReviewCount {NeedsReviewCount}, ElapsedMilliseconds {ElapsedMilliseconds}.",
                    requestId,
                    gigId,
                    request.UseAi,
                    matches.Count,
                    matches.Count(match => match.Status == ForScoreMappingStatus.Suggested),
                    matches.Count(match => match.Status == ForScoreMappingStatus.NeedsReview),
                    stopwatch.ElapsedMilliseconds);
                return Results.Ok(new SetListDraftChartMatchPreviewResponse(matches));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested || httpContext.RequestAborted.IsCancellationRequested)
            {
                logger.LogWarning(
                    "Set list chart match preview cancelled: RequestId {RequestId}, GigId {GigId}, UseAi {UseAi}, ElapsedMilliseconds {ElapsedMilliseconds}.",
                    requestId,
                    gigId,
                    request.UseAi,
                    stopwatch.ElapsedMilliseconds);
                throw;
            }
        });

        group.MapPost("/{gigId:guid}/setlist-imports/chart-matches/ai-jobs", async (
            Guid gigId,
            SetListDraftChartMatchJobRequest request,
            AppDbContext db,
            ClaimsPrincipal user,
            ICurrentUserAccessor currentUserAccessor,
            ISetListChartMatchJobQueue jobQueue,
            TimeProvider timeProvider,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var userId = currentUserAccessor.TryGetUserId(user);
            if (!userId.HasValue)
            {
                return Results.Unauthorized();
            }

            if (!await db.Gigs.WhereVisibleTo(userId).AnyAsync(gig => gig.Id == gigId, cancellationToken))
            {
                return Results.NotFound();
            }

            var validation = ValidateChartMatchJobRequest(request);
            if (validation is not null)
            {
                return validation;
            }

            var requestId = httpContext.Request.Headers.TryGetValue("X-Glovelly-Request-Id", out var requestIdHeader) && !string.IsNullOrWhiteSpace(requestIdHeader)
                ? requestIdHeader.ToString()
                : httpContext.TraceIdentifier;
            var now = timeProvider.GetUtcNow();
            var job = new SetListChartMatchJob
            {
                Id = Guid.NewGuid(),
                UserId = userId.Value,
                GigId = gigId,
                Status = SetListChartMatchJobStatus.Pending,
                InputJson = JsonSerializer.Serialize(request.Items.Select(ToMatchInput).ToList(), JsonOptions),
                CorrelationId = requestId,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
            };

            db.SetListChartMatchJobs.Add(job);
            await db.SaveChangesAsync(cancellationToken);
            await jobQueue.EnqueueAsync(job.Id, cancellationToken);

            return Results.Accepted(
                $"/gigs/{gigId}/setlist-imports/chart-matches/ai-jobs/{job.Id}",
                ToJobResponse(job, null));
        });

        group.MapGet("/{gigId:guid}/setlist-imports/chart-matches/ai-jobs/{jobId:guid}", async (
            Guid gigId,
            Guid jobId,
            AppDbContext db,
            ClaimsPrincipal user,
            ICurrentUserAccessor currentUserAccessor,
            CancellationToken cancellationToken) =>
        {
            var userId = currentUserAccessor.TryGetUserId(user);
            if (!userId.HasValue)
            {
                return Results.Unauthorized();
            }

            var job = await db.SetListChartMatchJobs
                .AsNoTracking()
                .FirstOrDefaultAsync(value => value.Id == jobId && value.GigId == gigId && value.UserId == userId.Value, cancellationToken);
            if (job is null)
            {
                return Results.NotFound();
            }

            IReadOnlyList<SetListChartMatchResult>? result = null;
            if (job.Status == SetListChartMatchJobStatus.Completed && !string.IsNullOrWhiteSpace(job.ResultJson))
            {
                result = JsonSerializer.Deserialize<IReadOnlyList<SetListChartMatchResult>>(job.ResultJson, JsonOptions);
            }

            return Results.Ok(ToJobResponse(job, result));
        });

        group.MapPost("/{gigId:guid}/setlist-imports", async (
            Guid gigId,
            SetListSaveImportRequest request,
            AppDbContext db,
            ClaimsPrincipal user,
            ICurrentUserAccessor currentUserAccessor,
            TimeProvider timeProvider,
            IWorkspaceEventPublisher workspaceEventPublisher,
            CancellationToken cancellationToken) =>
        {
            var userId = currentUserAccessor.TryGetUserId(user);
            var sourceResult = await ResolveSourceAsync(db, userId, gigId, request.ResourceId, cancellationToken);
            if (sourceResult.Result is not null)
            {
                return sourceResult.Result;
            }

            var validation = ValidateSaveRequest(request);
            if (validation is not null)
            {
                return validation;
            }

            var chartValidation = await ValidateRequestedChartsAsync(db, userId, request.Items.Select(item => item.ForScoreChartId), cancellationToken);
            if (chartValidation.Result is not null)
            {
                return chartValidation.Result;
            }

            var activeImportExists = await db.GigSetListImports
                .AnyAsync(value => value.GigId == gigId && value.IsActive, cancellationToken);
            if (activeImportExists && !request.ReplaceActiveImport)
            {
                return Results.Conflict(new
                {
                    message = "This gig already has an active setlist import. Confirm replacement before saving a new active import.",
                });
            }

            var now = DateTimeOffset.UtcNow;
            if (request.ReplaceActiveImport)
            {
                var activeImports = await db.GigSetListImports
                    .Where(value => value.GigId == gigId && value.IsActive)
                    .ToListAsync(cancellationToken);
                foreach (var activeImport in activeImports)
                {
                    activeImport.IsActive = false;
                }
            }

            var import = new GigSetListImport
            {
                Id = Guid.NewGuid(),
                GigId = gigId,
                GigExternalResourceId = sourceResult.Resource!.Id,
                SpreadsheetId = sourceResult.SpreadsheetId!,
                WorksheetId = string.IsNullOrWhiteSpace(request.WorksheetId) ? null : request.WorksheetId.Trim(),
                WorksheetName = request.WorksheetName.Trim(),
                SourceUrl = sourceResult.Resource.Url,
                IsActive = true,
                ImportedAtUtc = now,
                CreatedAtUtc = now,
                Items = request.Items
                    .OrderBy(item => item.SortOrder)
                    .Select((item, index) => new GigSetListItem
                    {
                        Id = Guid.NewGuid(),
                        SortOrder = index,
                        SourceRowNumber = item.SourceRowNumber,
                        Kind = item.Kind,
                        Include = item.Include && item.Kind == GigSetListItemKind.Song,
                        Section = NormalizeOptional(item.Section),
                        PadNumber = NormalizeOptional(item.PadNumber),
                        Key = NormalizeOptional(item.Key),
                        Title = item.Title.Trim(),
                        Notes = NormalizeOptional(item.Notes),
                        RawCellsJson = NormalizeRawCellsJson(item.RawCellsJson),
                        Confidence = item.Confidence,
                        ForScoreChartId = item.ForScoreChartId,
                        ForScoreLibrarySnapshotId = item.ForScoreChartId.HasValue ? chartValidation.ChartsById[item.ForScoreChartId.Value].ForScoreLibrarySnapshotId : null,
                        ForScoreChartTitle = item.ForScoreChartId.HasValue ? chartValidation.ChartsById[item.ForScoreChartId.Value].Title : null,
                        ForScoreChartFilePath = item.ForScoreChartId.HasValue ? chartValidation.ChartsById[item.ForScoreChartId.Value].FilePath : null,
                        ForScoreMappingStatus = item.ForScoreChartId.HasValue ? ForScoreMappingStatus.Linked : ForScoreMappingStatus.Unmapped,
                        ForScoreMappingConfidence = item.ForScoreChartId.HasValue ? ForScoreMappingConfidence.Manual : ForScoreMappingConfidence.None,
                        ForScoreMappingUpdatedAtUtc = item.ForScoreChartId.HasValue ? timeProvider.GetUtcNow() : null,
                        ForScoreMatchJson = SerializeMatch(item.ForScoreMatch),
                    })
                    .ToList(),
            };

            db.GigSetListImports.Add(import);
            var gig = await db.Gigs.WhereVisibleTo(userId).FirstAsync(value => value.Id == gigId, cancellationToken);
            EndpointSupport.StampUpdate(gig, userId);
            await db.SaveChangesAsync(cancellationToken);
            await workspaceEventPublisher.PublishAsync(userId, new WorkspaceEvent("gigs", "updated", gigId, DateTimeOffset.UtcNow));

            return Results.Created($"/gigs/{gigId}/setlist-imports/{import.Id}", ToImportResponse(import));
        });

        group.MapPut("/{gigId:guid}/setlist-imports/{importId:guid}", async (
            Guid gigId,
            Guid importId,
            SetListUpdateImportRequest request,
            AppDbContext db,
            ClaimsPrincipal user,
            ICurrentUserAccessor currentUserAccessor,
            TimeProvider timeProvider,
            IWorkspaceEventPublisher workspaceEventPublisher,
            CancellationToken cancellationToken) =>
        {
            var userId = currentUserAccessor.TryGetUserId(user);
            var gig = await db.Gigs
                .WhereVisibleTo(userId)
                .FirstOrDefaultAsync(value => value.Id == gigId, cancellationToken);
            if (gig is null)
            {
                return Results.NotFound();
            }

            var import = await db.GigSetListImports
                .Include(value => value.Items)
                .FirstOrDefaultAsync(value => value.Id == importId && value.GigId == gigId, cancellationToken);
            if (import is null)
            {
                return Results.NotFound();
            }

            var validation = ValidateUpdateRequest(request, import.Items);
            if (validation is not null)
            {
                return validation;
            }

            var chartValidation = await ValidateRequestedChartsAsync(db, userId, request.Items.Select(item => item.ForScoreChartId), cancellationToken);
            if (chartValidation.Result is not null)
            {
                return chartValidation.Result;
            }

            var itemsById = import.Items.ToDictionary(value => value.Id);
            foreach (var requestItem in request.Items.OrderBy(value => value.SortOrder))
            {
                var item = itemsById[requestItem.Id];
                item.SortOrder = requestItem.SortOrder;
                item.Kind = requestItem.Kind;
                item.Include = requestItem.Include && requestItem.Kind == GigSetListItemKind.Song;
                item.Section = NormalizeOptional(requestItem.Section);
                item.PadNumber = NormalizeOptional(requestItem.PadNumber);
                item.Key = NormalizeOptional(requestItem.Key);
                item.Title = requestItem.Title.Trim();
                item.Notes = NormalizeOptional(requestItem.Notes);
                item.Confidence = requestItem.Confidence;
                item.ForScoreMatchJson = SerializeMatch(requestItem.ForScoreMatch);
                ApplyChartMapping(item, requestItem.ForScoreChartId, chartValidation.ChartsById, timeProvider.GetUtcNow());
            }

            EndpointSupport.StampUpdate(gig, userId);
            await db.SaveChangesAsync(cancellationToken);
            await workspaceEventPublisher.PublishAsync(userId, new WorkspaceEvent("gigs", "updated", gigId, DateTimeOffset.UtcNow));

            return Results.Ok(ToImportResponse(import));
        });

        group.MapPost("/{gigId:guid}/setlist-imports/{importId:guid}/chart-matches/preview", async (
            Guid gigId,
            Guid importId,
            SetListSavedChartMatchPreviewRequest request,
            AppDbContext db,
            ClaimsPrincipal user,
            ICurrentUserAccessor currentUserAccessor,
            ISetListChartMatcher chartMatcher,
            CancellationToken cancellationToken) =>
        {
            var userId = currentUserAccessor.TryGetUserId(user);
            var gig = await db.Gigs.WhereVisibleTo(userId).FirstOrDefaultAsync(value => value.Id == gigId, cancellationToken);
            if (gig is null)
            {
                return Results.NotFound();
            }

            var import = await db.GigSetListImports
                .AsNoTracking()
                .Include(value => value.Items)
                .FirstOrDefaultAsync(value => value.Id == importId && value.GigId == gigId, cancellationToken);
            if (import is null)
            {
                return Results.NotFound();
            }

            var matches = await chartMatcher.MatchAsync(userId, import.Items.OrderBy(item => item.SortOrder).Select(ToMatchInput).ToList(), cancellationToken, request.UseAi);
            return Results.Ok(new SetListChartMatchPreviewResponse(import.Id, matches));
        });

        return group;
    }

    private static async Task<GoogleConnection?> ResolveSheetsConnectionAsync(
        IGoogleConnectionService googleConnectionService,
        Guid? userId,
        CancellationToken cancellationToken)
    {
        return userId.HasValue
            ? await googleConnectionService.GetActiveConnectionAsync(userId.Value, [GoogleScopes.SpreadsheetsReadonly], cancellationToken)
            : null;
    }

    private static async Task<SetListSourceResolution> ResolveSourceAsync(
        AppDbContext db,
        Guid? userId,
        Guid gigId,
        Guid? resourceId,
        CancellationToken cancellationToken)
    {
        var gig = await db.Gigs
            .WhereVisibleTo(userId)
            .Include(value => value.ExternalResources)
            .FirstOrDefaultAsync(value => value.Id == gigId, cancellationToken);
        if (gig is null)
        {
            return new SetListSourceResolution(null, null, Results.NotFound());
        }

        var resource = resourceId.HasValue
            ? gig.ExternalResources.FirstOrDefault(value => value.Id == resourceId.Value)
            : gig.ExternalResources
                .Where(value => value.ResourceType == GigExternalResourceType.GoogleSheet && value.Purpose == GigExternalResourcePurpose.SetList)
                .OrderByDescending(value => value.IsPrimary)
                .ThenBy(value => value.Title)
                .FirstOrDefault();
        if (resource is null)
        {
            return new SetListSourceResolution(null, null, EndpointSupport.ValidationProblem("resourceId", "Choose a linked Google Sheet setlist resource first."));
        }

        if (resource.ResourceType != GigExternalResourceType.GoogleSheet || resource.Purpose != GigExternalResourcePurpose.SetList)
        {
            return new SetListSourceResolution(null, null, EndpointSupport.ValidationProblem("resourceId", "The source resource must be a Google Sheet setlist."));
        }

        if (string.IsNullOrWhiteSpace(resource.Url) || !TryParseSpreadsheetId(resource.Url, out var spreadsheetId))
        {
            return new SetListSourceResolution(null, null, EndpointSupport.ValidationProblem("url", "The Google Sheet URL does not include a spreadsheet ID."));
        }

        return new SetListSourceResolution(resource, spreadsheetId, null);
    }

    private static async Task<WorksheetNameResolution> ResolveWorksheetNameAsync(
        IGoogleSheetsApiClient sheetsApiClient,
        GoogleConnectionAccessToken accessToken,
        string spreadsheetId,
        string? worksheetName,
        string? worksheetId,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(worksheetName))
        {
            return new WorksheetNameResolution(worksheetName.Trim(), null);
        }

        var metadataResult = await ReadSpreadsheetMetadataAsync(sheetsApiClient, accessToken, spreadsheetId, cancellationToken);
        if (metadataResult.Result is not null)
        {
            return new WorksheetNameResolution(null, metadataResult.Result);
        }

        var metadata = metadataResult.Metadata!;
        if (!string.IsNullOrWhiteSpace(worksheetId))
        {
            var matchingSheet = metadata.Sheets.FirstOrDefault(sheet => sheet.SheetId == worksheetId.Trim());
            if (matchingSheet is not null)
            {
                return new WorksheetNameResolution(matchingSheet.Title, null);
            }
        }

        var firstSheet = metadata.Sheets.FirstOrDefault();
        return firstSheet is null
            ? new WorksheetNameResolution(null, NoWorksheetsProblem())
            : new WorksheetNameResolution(firstSheet.Title, null);
    }

    private static async Task<SheetsAccessTokenResolution> ResolveSheetsAccessTokenAsync(
        IGoogleConnectionService googleConnectionService,
        GoogleConnection connection,
        CancellationToken cancellationToken)
    {
        try
        {
            var accessToken = await googleConnectionService.GetAccessTokenAsync(
                connection,
                [GoogleScopes.SpreadsheetsReadonly],
                cancellationToken);
            return new SheetsAccessTokenResolution(accessToken, null);
        }
        catch (InvalidOperationException exception)
        {
            return new SheetsAccessTokenResolution(null, SheetsReconnectProblem(exception.Message));
        }
    }

    private static async Task<SpreadsheetMetadataResolution> ReadSpreadsheetMetadataAsync(
        IGoogleSheetsApiClient sheetsApiClient,
        GoogleConnectionAccessToken accessToken,
        string spreadsheetId,
        CancellationToken cancellationToken)
    {
        try
        {
            var metadata = await sheetsApiClient.GetSpreadsheetMetadataAsync(accessToken, spreadsheetId, cancellationToken);
            if (metadata.Sheets.Count == 0)
            {
                return new SpreadsheetMetadataResolution(null, NoWorksheetsProblem());
            }

            return new SpreadsheetMetadataResolution(metadata, null);
        }
        catch (InvalidOperationException exception)
        {
            return new SpreadsheetMetadataResolution(null, SheetsReadProblem(
                "Google Sheet could not be read",
                $"The linked Google Sheet could not be read. {exception.Message}"));
        }
    }

    private static async Task<WorksheetValuesResolution> ReadWorksheetValuesAsync(
        IGoogleSheetsApiClient sheetsApiClient,
        GoogleConnectionAccessToken accessToken,
        string spreadsheetId,
        string worksheetName,
        CancellationToken cancellationToken)
    {
        try
        {
            var values = await sheetsApiClient.GetWorksheetValuesAsync(accessToken, spreadsheetId, worksheetName, cancellationToken);
            return new WorksheetValuesResolution(values, null);
        }
        catch (InvalidOperationException exception)
        {
            return new WorksheetValuesResolution(null, SheetsReadProblem(
                "Google Sheet worksheet could not be read",
                $"The selected worksheet rows could not be read. {exception.Message}"));
        }
    }

    private static IResult SheetsReconnectProblem(string detail)
    {
        return Results.Problem(
            title: "Google Sheets must be reconnected",
            detail: $"Reconnect Google Sheets and try again. {detail}".Trim(),
            statusCode: StatusCodes.Status409Conflict);
    }

    private static IResult SheetsReadProblem(string title, string detail)
    {
        return Results.Problem(
            title: title,
            detail: detail,
            statusCode: StatusCodes.Status502BadGateway);
    }

    private static IResult NoWorksheetsProblem()
    {
        return Results.Problem(
            title: "Google Sheet has no worksheets",
            detail: "The linked Google Sheet did not include any worksheets to import.",
            statusCode: StatusCodes.Status422UnprocessableEntity);
    }

    private static IResult? ValidateSaveRequest(SetListSaveImportRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.WorksheetName))
        {
            errors["worksheetName"] = ["Worksheet name is required."];
        }

        if (request.Items.Count == 0)
        {
            errors["items"] = ["Import at least one reviewed setlist row."];
        }

        for (var index = 0; index < request.Items.Count; index++)
        {
            var item = request.Items[index];
            if (!Enum.IsDefined(item.Kind))
            {
                errors[$"items[{index}].kind"] = ["Item kind is invalid."];
            }

            if (!Enum.IsDefined(item.Confidence))
            {
                errors[$"items[{index}].confidence"] = ["Item confidence is invalid."];
            }

            if (string.IsNullOrWhiteSpace(item.Title))
            {
                errors[$"items[{index}].title"] = ["Title is required."];
            }
        }

        return errors.Count == 0 ? null : Results.ValidationProblem(errors);
    }

    private static IResult? ValidateUpdateRequest(SetListUpdateImportRequest request, IEnumerable<GigSetListItem> existingItems)
    {
        var errors = new Dictionary<string, string[]>();
        var existingIds = existingItems.Select(value => value.Id).ToHashSet();
        var requestedIds = request.Items.Select(value => value.Id).ToList();
        if (requestedIds.Count != existingIds.Count || requestedIds.Distinct().Count() != existingIds.Count || requestedIds.Any(id => !existingIds.Contains(id)))
        {
            errors["items"] = ["Updated setlist items must match the saved import items."];
        }

        for (var index = 0; index < request.Items.Count; index++)
        {
            var item = request.Items[index];
            if (!Enum.IsDefined(item.Kind))
            {
                errors[$"items[{index}].kind"] = ["Item kind is invalid."];
            }

            if (!Enum.IsDefined(item.Confidence))
            {
                errors[$"items[{index}].confidence"] = ["Item confidence is invalid."];
            }

            if (string.IsNullOrWhiteSpace(item.Title))
            {
                errors[$"items[{index}].title"] = ["Title is required."];
            }
        }

        return errors.Count == 0 ? null : Results.ValidationProblem(errors);
    }

    private static IResult? ValidateChartMatchJobRequest(SetListDraftChartMatchJobRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        if (request.Items.Count == 0)
        {
            errors["items"] = ["Provide setlist rows before starting chart matching."];
        }

        for (var index = 0; index < request.Items.Count; index++)
        {
            var item = request.Items[index];
            if (item.SourceRowNumber <= 0)
            {
                errors[$"items[{index}].sourceRowNumber"] = ["Source row number is required."];
            }

            if (!Enum.IsDefined(item.Kind))
            {
                errors[$"items[{index}].kind"] = ["Item kind is invalid."];
            }
        }

        if (!request.Items.Any(item => item.Kind == GigSetListItemKind.Song && item.Include))
        {
            errors["items"] = ["Include at least one song row before starting chart matching."];
        }

        return errors.Count == 0 ? null : Results.ValidationProblem(errors);
    }

    private static bool TryParseSpreadsheetId(string url, out string spreadsheetId)
    {
        spreadsheetId = string.Empty;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || !string.Equals(uri.Host, "docs.google.com", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var spreadsheetSegmentIndex = Array.FindIndex(segments, segment => string.Equals(segment, "d", StringComparison.OrdinalIgnoreCase));
        if (spreadsheetSegmentIndex < 0 || spreadsheetSegmentIndex + 1 >= segments.Length)
        {
            return false;
        }

        spreadsheetId = segments[spreadsheetSegmentIndex + 1];
        return !string.IsNullOrWhiteSpace(spreadsheetId);
    }

    private static string NormalizeRawCellsJson(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "[]";
        }

        try
        {
            using var document = JsonDocument.Parse(value);
            return JsonSerializer.Serialize(document.RootElement, JsonOptions);
        }
        catch (JsonException)
        {
            return "[]";
        }
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static SetListImportResponse ToImportResponse(GigSetListImport import)
    {
        return new SetListImportResponse(
            import.Id,
            import.GigId,
            import.GigExternalResourceId,
            import.SpreadsheetId,
            import.WorksheetId,
            import.WorksheetName,
            import.SourceUrl,
            import.IsActive,
            import.ImportedAtUtc,
            import.Items.OrderBy(item => item.SortOrder).Select(ToItemResponse).ToList());
    }

    private static SetListImportItemResponse ToItemResponse(GigSetListItem item)
    {
        return new SetListImportItemResponse(
            item.Id,
            item.SourceRowNumber,
            item.SortOrder,
            item.Kind,
            item.Include,
            item.Section,
            item.PadNumber,
            item.Key,
            item.Title,
            item.Notes,
            item.RawCellsJson,
            item.Confidence,
            item.ForScoreChartId,
            DeserializeMatch(item.ForScoreMatchJson),
            new SetListSavedChartMappingResponse(
                item.ForScoreLibrarySnapshotId,
                item.ForScoreChartId,
                item.ForScoreChartTitle,
                item.ForScoreChartFilePath,
                item.ForScoreMappingStatus,
                item.ForScoreMappingConfidence,
                item.ForScoreMappingUpdatedAtUtc));
    }

    private static SetListChartMatchInput ToMatchInput(SetListImportItemDraft item) => new(
        null,
        item.SourceRowNumber,
        item.Kind,
        item.Include,
        item.Title,
        item.PadNumber,
        item.Key);

    private static SetListChartMatchInput ToMatchInput(SetListDraftChartMatchPreviewItem item) => new(
        null,
        item.SourceRowNumber,
        item.Kind,
        item.Include,
        item.Title,
        item.PadNumber,
        item.Key);

    private static SetListChartMatchInput ToMatchInput(SetListDraftChartMatchJobItem item) => new(
        null,
        item.SourceRowNumber,
        item.Kind,
        item.Include,
        item.Title,
        item.PadNumber,
        item.Key);

    private static SetListChartMatchInput ToMatchInput(GigSetListItem item) => new(
        item.Id,
        item.SourceRowNumber,
        item.Kind,
        item.Include,
        item.Title,
        item.PadNumber,
        item.Key);

    private static string? SerializeMatch(SetListChartMatchResult? match)
    {
        return match is null ? null : JsonSerializer.Serialize(match, JsonOptions);
    }

    private static SetListChartMatchResult? DeserializeMatch(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<SetListChartMatchResult>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static SetListChartMatchJobResponse ToJobResponse(
        SetListChartMatchJob job,
        IReadOnlyList<SetListChartMatchResult>? result) => new(
            job.Id,
            job.GigId,
            job.Status,
            job.CorrelationId,
            job.SafeErrorMessage,
            job.CreatedAtUtc,
            job.UpdatedAtUtc,
            job.StartedAtUtc,
            job.CompletedAtUtc,
            result);

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private static async Task<ChartValidationResult> ValidateRequestedChartsAsync(
        AppDbContext db,
        Guid? userId,
        IEnumerable<Guid?> requestedChartIds,
        CancellationToken cancellationToken)
    {
        var ids = requestedChartIds.Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
        if (ids.Count == 0)
        {
            return new ChartValidationResult(new Dictionary<Guid, ForScoreChart>(), null);
        }

        var charts = await db.ForScoreCharts
            .Include(chart => chart.Snapshot)
            .Where(chart => ids.Contains(chart.Id)
                && chart.Snapshot != null
                && chart.Snapshot.CreatedByUserId == userId
                && chart.Snapshot.IsActive)
            .ToListAsync(cancellationToken);

        if (charts.Count != ids.Count)
        {
            return new ChartValidationResult(new Dictionary<Guid, ForScoreChart>(), EndpointSupport.ValidationProblem("forScoreChartId", "Choose charts from your active forScore library."));
        }

        return new ChartValidationResult(charts.ToDictionary(chart => chart.Id), null);
    }

    private static void ApplyChartMapping(
        GigSetListItem item,
        Guid? chartId,
        IReadOnlyDictionary<Guid, ForScoreChart> chartsById,
        DateTimeOffset now)
    {
        if (!chartId.HasValue)
        {
            item.ForScoreChartId = null;
            item.ForScoreLibrarySnapshotId = null;
            item.ForScoreChartTitle = null;
            item.ForScoreChartFilePath = null;
            item.ForScoreMappingStatus = item.Kind == GigSetListItemKind.Song && item.Include
                ? ForScoreMappingStatus.Unmapped
                : ForScoreMappingStatus.NotApplicable;
            item.ForScoreMappingConfidence = ForScoreMappingConfidence.None;
            item.ForScoreMappingUpdatedAtUtc = now;
            return;
        }

        var chart = chartsById[chartId.Value];
        item.ForScoreChartId = chart.Id;
        item.ForScoreLibrarySnapshotId = chart.ForScoreLibrarySnapshotId;
        item.ForScoreChartTitle = chart.Title;
        item.ForScoreChartFilePath = chart.FilePath;
        item.ForScoreMappingStatus = ForScoreMappingStatus.Linked;
        item.ForScoreMappingConfidence = ForScoreMappingConfidence.Manual;
        item.ForScoreMappingUpdatedAtUtc = now;
    }

    private sealed record SetListSourceResolution(GigExternalResource? Resource, string? SpreadsheetId, IResult? Result);

    private sealed record SheetsAccessTokenResolution(GoogleConnectionAccessToken? AccessToken, IResult? Result);

    private sealed record SpreadsheetMetadataResolution(GoogleSpreadsheetMetadata? Metadata, IResult? Result);

    private sealed record WorksheetNameResolution(string? WorksheetName, IResult? Result);

    private sealed record WorksheetValuesResolution(GoogleSheetValues? Values, IResult? Result);

    private sealed record SetListSourceResponse(
        Guid ResourceId,
        string ResourceTitle,
        string ResourceUrl,
        string SpreadsheetId,
        IReadOnlyList<GoogleSheetMetadata> Worksheets);

    private sealed record SetListPreviewRequest(Guid? ResourceId, string? WorksheetId, string? WorksheetName);

    private sealed record SetListPreviewResponse(
        Guid ResourceId,
        string ResourceTitle,
        string ResourceUrl,
        string SpreadsheetId,
        string? WorksheetId,
        string WorksheetName,
        IReadOnlyList<SetListImportItemDraft> Items);

    private sealed record SetListSaveImportRequest(
        Guid? ResourceId,
        string? WorksheetId,
        string WorksheetName,
        bool ReplaceActiveImport,
        IReadOnlyList<SetListImportItemDraft> Items);

    private sealed record SetListDraftChartMatchPreviewRequest(IReadOnlyList<SetListDraftChartMatchPreviewItem> Items, bool UseAi = true);

    private sealed record SetListDraftChartMatchPreviewItem(
        int SourceRowNumber,
        GigSetListItemKind Kind,
        bool Include,
        string Title,
        string? PadNumber,
        string? Key);

    private sealed record SetListDraftChartMatchPreviewResponse(IReadOnlyList<SetListChartMatchResult> Items);

    private sealed record SetListDraftChartMatchJobRequest(IReadOnlyList<SetListDraftChartMatchJobItem> Items);

    private sealed record SetListDraftChartMatchJobItem(
        int SourceRowNumber,
        GigSetListItemKind Kind,
        bool Include,
        string Title,
        string? PadNumber,
        string? Key);

    private sealed record SetListChartMatchJobResponse(
        Guid JobId,
        Guid GigId,
        SetListChartMatchJobStatus Status,
        string? CorrelationId,
        string? ErrorMessage,
        DateTimeOffset CreatedAtUtc,
        DateTimeOffset UpdatedAtUtc,
        DateTimeOffset? StartedAtUtc,
        DateTimeOffset? CompletedAtUtc,
        IReadOnlyList<SetListChartMatchResult>? Result);

    private sealed record UnexportableSetListItemResponse(Guid Id, int SourceRowNumber, string Title);

    private sealed record SetListUpdateImportRequest(IReadOnlyList<SetListUpdateImportItemRequest> Items);

    private sealed record SetListUpdateImportItemRequest(
        Guid Id,
        int SourceRowNumber,
        int SortOrder,
        GigSetListItemKind Kind,
        bool Include,
        string? Section,
        string? PadNumber,
        string? Key,
        string Title,
        string? Notes,
        string RawCellsJson,
        GigSetListItemConfidence Confidence,
        Guid? ForScoreChartId,
        SetListChartMatchResult? ForScoreMatch);

    private sealed record SetListImportResponse(
        Guid Id,
        Guid GigId,
        Guid? ResourceId,
        string SpreadsheetId,
        string? WorksheetId,
        string WorksheetName,
        string? SourceUrl,
        bool IsActive,
        DateTimeOffset ImportedAtUtc,
        IReadOnlyList<SetListImportItemResponse> Items);

    private sealed record SetListImportItemResponse(
        Guid Id,
        int SourceRowNumber,
        int SortOrder,
        GigSetListItemKind Kind,
        bool Include,
        string? Section,
        string? PadNumber,
        string? Key,
        string Title,
        string? Notes,
        string RawCellsJson,
        GigSetListItemConfidence Confidence,
        Guid? ForScoreChartId,
        SetListChartMatchResult? ForScoreMatch,
        SetListSavedChartMappingResponse ForScoreMapping);

    private sealed record SetListSavedChartMappingResponse(
        Guid? SnapshotId,
        Guid? ChartId,
        string? ChartTitle,
        string? ChartFilePath,
        ForScoreMappingStatus Status,
        ForScoreMappingConfidence Confidence,
        DateTimeOffset? UpdatedAtUtc);

    private sealed record SetListSavedChartMatchPreviewRequest(bool UseAi = true);

    private sealed record SetListChartMatchPreviewResponse(Guid ImportId, IReadOnlyList<SetListChartMatchResult> Items);

    private sealed record ChartValidationResult(IReadOnlyDictionary<Guid, ForScoreChart> ChartsById, IResult? Result);
}
