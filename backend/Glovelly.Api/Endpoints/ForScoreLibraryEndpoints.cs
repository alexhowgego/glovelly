using System.Security.Claims;
using System.Text.Json;
using Glovelly.Api.Auth;
using Glovelly.Api.Data;
using Glovelly.Api.Models;
using Glovelly.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace Glovelly.Api.Endpoints;

internal static class ForScoreLibraryEndpoints
{
    private const long MaxUploadBytes = 50 * 1024 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static WebApplication MapForScoreLibraryEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/forscore-library")
            .WithTags("forScore library")
            .RequireAuthorization(GlovellyPolicies.GlovellyUser);

        group.MapGet("/active", async (
            AppDbContext db,
            ClaimsPrincipal user,
            ICurrentUserAccessor currentUserAccessor,
            CancellationToken cancellationToken) =>
        {
            var userId = currentUserAccessor.TryGetUserId(user);
            var snapshot = await db.ForScoreLibrarySnapshots
                .AsNoTracking()
                .Where(value => value.CreatedByUserId == userId && value.IsActive)
                .OrderByDescending(value => value.ImportedAtUtc)
                .FirstOrDefaultAsync(cancellationToken);

            return snapshot is null ? Results.NotFound() : Results.Ok(ToSnapshotResponse(snapshot));
        });

        group.MapGet("/active/charts", async (
            AppDbContext db,
            ClaimsPrincipal user,
            ICurrentUserAccessor currentUserAccessor,
            CancellationToken cancellationToken) =>
        {
            var userId = currentUserAccessor.TryGetUserId(user);
            var snapshot = await db.ForScoreLibrarySnapshots
                .AsNoTracking()
                .Include(value => value.Charts)
                .Where(value => value.CreatedByUserId == userId && value.IsActive)
                .OrderByDescending(value => value.ImportedAtUtc)
                .FirstOrDefaultAsync(cancellationToken);

            return snapshot is null
                ? Results.NotFound()
                : Results.Ok(new ForScoreLibraryChartsResponse(
                    snapshot.Id,
                    snapshot.Charts.OrderBy(chart => chart.SortOrder).Select(ToChartResponse).ToList()));
        });

        group.MapPost("/imports", async (
            HttpRequest request,
            ClaimsPrincipal user,
            ICurrentUserAccessor currentUserAccessor,
            IForScoreLibraryImportService importService,
            CancellationToken cancellationToken) =>
        {
            if (!request.HasFormContentType)
            {
                return EndpointSupport.ValidationProblem("file", "Upload a forScore .4sb library export.");
            }

            var form = await request.ReadFormAsync(cancellationToken);
            var file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
            var validation = ValidateUpload(file);
            if (validation is not null)
            {
                return validation;
            }

            var userId = currentUserAccessor.TryGetUserId(user);
            try
            {
                await using var stream = file!.OpenReadStream();
                var snapshot = await importService.ImportAsync(userId!.Value, file.FileName, stream, cancellationToken);
                return Results.Created($"/forscore-library/imports/{snapshot.Id}", ToSnapshotResponse(snapshot));
            }
            catch (ForScoreLibraryParseException exception)
            {
                return EndpointSupport.ValidationProblem("file", exception.Message);
            }
            catch (InvalidDataException)
            {
                return EndpointSupport.ValidationProblem("file", "The uploaded forScore library export could not be read.");
            }
        });

        return app;
    }

    private static IResult? ValidateUpload(IFormFile? file)
    {
        if (file is null || file.Length == 0)
        {
            return EndpointSupport.ValidationProblem("file", "Upload a forScore .4sb library export.");
        }

        if (file.Length > MaxUploadBytes)
        {
            return EndpointSupport.ValidationProblem("file", $"forScore library exports must be {MaxUploadBytes / 1024 / 1024} MB or smaller.");
        }

        if (!Path.GetExtension(file.FileName).Equals(".4sb", StringComparison.OrdinalIgnoreCase))
        {
            return EndpointSupport.ValidationProblem("file", "Upload a .4sb file exported by forScore.");
        }

        return null;
    }

    private static ForScoreLibrarySnapshotResponse ToSnapshotResponse(ForScoreLibrarySnapshot snapshot)
    {
        var warnings = JsonSerializer.Deserialize<IReadOnlyList<string>>(snapshot.WarningsJson, JsonOptions) ?? [];
        return new ForScoreLibrarySnapshotResponse(
            snapshot.Id,
            snapshot.OriginalFileName,
            snapshot.SourceFormat,
            snapshot.BackupVersion,
            snapshot.IsActive,
            snapshot.ChartCount,
            warnings,
            snapshot.ImportedAtUtc,
            snapshot.CreatedAtUtc);
    }

    private static ForScoreChartResponse ToChartResponse(ForScoreChart chart) => new(
        chart.Id,
        chart.FilePath,
        chart.Title,
        chart.NormalizedTitle,
        chart.Keywords,
        chart.AddedAt,
        chart.PrintNumber,
        chart.Version);

    private sealed record ForScoreLibrarySnapshotResponse(
        Guid Id,
        string OriginalFileName,
        string SourceFormat,
        string? BackupVersion,
        bool IsActive,
        int ChartCount,
        IReadOnlyList<string> Warnings,
        DateTimeOffset ImportedAtUtc,
        DateTimeOffset CreatedAtUtc);

    private sealed record ForScoreLibraryChartsResponse(
        Guid SnapshotId,
        IReadOnlyList<ForScoreChartResponse> Charts);

    private sealed record ForScoreChartResponse(
        Guid Id,
        string FilePath,
        string Title,
        string NormalizedTitle,
        string? Keywords,
        DateTimeOffset? AddedAt,
        int? PrintNumber,
        int? Version);
}
