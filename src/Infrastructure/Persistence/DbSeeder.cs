using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShiftLedger.Application.Common.Interfaces;
using ShiftLedger.Domain.Entities;
using ShiftLedger.Domain.Enums;

namespace ShiftLedger.Infrastructure.Persistence;

public static class DbSeeder
{
    // Creates a bootstrap Admin from config (BootstrapAdmin:Email/Password) if no users exist yet,
    // so the very first login has an account. No-op once any user exists.
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();

        if (await db.Users.AnyAsync()) return;

        var email = config["BootstrapAdmin:Email"];
        var password = config["BootstrapAdmin:Password"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password)) return;

        db.Users.Add(new User
        {
            FullName = "Administrator",
            Email = email.Trim().ToLowerInvariant(),
            PasswordHash = hasher.Hash(password),
            Role = Role.Admin,
        });
        await db.SaveChangesAsync();
    }
}
