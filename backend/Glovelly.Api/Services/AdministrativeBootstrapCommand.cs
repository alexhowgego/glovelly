using Glovelly.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Glovelly.Api.Services;

public static class AdministrativeBootstrapCommand
{
    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length != 7 || args[1] != "--user-id" || args[3] != "--operator" || args[5] != "--reason" ||
            !Guid.TryParse(args[2], out var userId) || userId == Guid.Empty ||
            string.IsNullOrWhiteSpace(args[4]) || string.IsNullOrWhiteSpace(args[6]))
        {
            Console.Error.WriteLine("Usage: grant-super-admin --user-id <guid> --operator <identity> --reason <reason>");
            return 2;
        }

        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__Glovelly");
        if (string.IsNullOrWhiteSpace(connection))
        {
            Console.Error.WriteLine("ConnectionStrings__Glovelly must explicitly identify the target database.");
            return 2;
        }

        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options;
        await using var db = new AppDbContext(options);
        var service = new AdministrativePermissionAssignmentService(db, new AdministrativeMutationCoordinator(db), TimeProvider.System);
        var result = await service.BootstrapSuperAsync(
            userId, new AdministrativeChangeActor(null, args[4].Trim(), "Operator"), args[6]);
        Console.WriteLine($"Super Administrator grant: {result}.");
        return result is AdministrativeAssignmentResult.Changed or AdministrativeAssignmentResult.Unchanged ? 0 : 1;
    }
}
