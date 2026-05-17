using Glovelly.Api.Auth;
using Glovelly.Api.Services;
using System.Security.Claims;
using System.Text.RegularExpressions;

namespace Glovelly.Api.Endpoints;

public static partial class ExpenseStatementEndpoints
{
    public static IEndpointRouteBuilder MapExpenseStatementEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/expense-statements")
            .WithTags("ExpenseStatements")
            .RequireAuthorization(GlovellyPolicies.GlovellyUser);

        group.MapPost("/preview", async (
            ExpenseStatementRequest request,
            ClaimsPrincipal user,
            ICurrentUserAccessor currentUserAccessor,
            IExpenseStatementBuilder builder,
            CancellationToken cancellationToken) =>
        {
            var userId = currentUserAccessor.TryGetUserId(user);
            try
            {
                var statement = await builder.BuildAsync(request, userId, cancellationToken);
                return Results.Ok(statement);
            }
            catch (ExpenseStatementValidationException exception)
            {
                return Results.ValidationProblem(exception.Errors);
            }
        });

        group.MapPost("/pdf", async (
            ExpenseStatementRequest request,
            ClaimsPrincipal user,
            ICurrentUserAccessor currentUserAccessor,
            IExpenseStatementBuilder builder,
            IExpenseStatementPdfRenderer renderer,
            CancellationToken cancellationToken) =>
        {
            var userId = currentUserAccessor.TryGetUserId(user);
            try
            {
                var statement = await builder.BuildAsync(request, userId, cancellationToken);
                var pdf = renderer.Render(statement, request.IncludeReceiptAppendix);
                return Results.File(
                    pdf,
                    "application/pdf",
                    BuildFilename(statement));
            }
            catch (ExpenseStatementValidationException exception)
            {
                return Results.ValidationProblem(exception.Errors);
            }
        });

        return app;
    }

    private static string BuildFilename(ExpenseStatementProjection statement)
    {
        var clientName = SanitizedFilenamePartRegex()
            .Replace(statement.ClientName.Trim(), "-")
            .Trim('-');

        if (string.IsNullOrWhiteSpace(clientName))
        {
            clientName = "Client";
        }

        return $"Expense-Statement-{clientName}-{statement.StatementDate:yyyyMMdd}.pdf";
    }

    [GeneratedRegex("[^A-Za-z0-9._-]+")]
    private static partial Regex SanitizedFilenamePartRegex();
}
