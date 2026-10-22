using MentalAccounting.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
namespace MentalAccounting.Web;

public static class AdminCommands
{
    public static async Task Run(IServiceProvider services, string command)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AccountingDbContext>();
        if (command == "migrate") { await db.Database.MigrateAsync(); Console.WriteLine("Migrazioni applicate."); return; }
        await using var transaction = await db.Database.BeginTransactionAsync();
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(82471921)");
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var user = await users.Users.SingleOrDefaultAsync();
        if (command == "create-user" && user is not null) throw new InvalidOperationException("Esiste già l'account personale.");
        if (command == "reset-password" && user is null) throw new InvalidOperationException("Creare prima l'account.");
        var password = Environment.GetEnvironmentVariable("ADMIN_PASSWORD");
        if (string.IsNullOrEmpty(password))
        {
            Console.Write("Password (12+ caratteri, maiuscole, minuscole, numeri e simboli): ");
            if (Console.IsInputRedirected) password = Console.ReadLine();
            else
            {
                var chars = new List<char>(); ConsoleKeyInfo key;
                while ((key = Console.ReadKey(true)).Key != ConsoleKey.Enter)
                { if (key.Key == ConsoleKey.Backspace) { if (chars.Count > 0) chars.RemoveAt(chars.Count - 1); } else if (!char.IsControl(key.KeyChar)) chars.Add(key.KeyChar); }
                password = new string(chars.ToArray()); Console.WriteLine();
            }
        }
        if (string.IsNullOrEmpty(password)) throw new InvalidOperationException("Password mancante.");
        IdentityResult result;
        if (user is null) result = await users.CreateAsync(new IdentityUser(Environment.GetEnvironmentVariable("ADMIN_USERNAME") ?? "admin"), password);
        else
        {
            result = await users.ResetPasswordAsync(user, await users.GeneratePasswordResetTokenAsync(user), password);
            if (result.Succeeded)
            {
                await users.SetLockoutEndDateAsync(user, null);
                await users.ResetAccessFailedCountAsync(user);
                await users.UpdateSecurityStampAsync(user);
            }
        }
        if (!result.Succeeded) throw new InvalidOperationException(string.Join("; ", result.Errors.Select(x => x.Description)));
        await transaction.CommitAsync(); Console.WriteLine("Account aggiornato.");
    }
}
public sealed class DatabaseHealthCheck(IServiceScopeFactory scopes) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AccountingDbContext>();
            return await db.Database.CanConnectAsync(cancellationToken) ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy();
        }
        catch { return HealthCheckResult.Unhealthy(); }
    }
}
