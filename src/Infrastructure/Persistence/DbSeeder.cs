using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShiftLedger.Application.Common.Interfaces;
using ShiftLedger.Domain.Entities;
using ShiftLedger.Domain.Enums;

namespace ShiftLedger.Infrastructure.Persistence;

public static class DbSeeder
{
    // A fixed demo account (Development only) backing a quick-login button in the client.
    public sealed record DemoAccount(string FullName, string Email, string Password, Role Role);

    // Passwords are dev/demo only and intentionally known (see ShiftLedger-Client/CREDENTIALS.md);
    // they must be rotated before any non-dev environment.
    public static readonly DemoAccount DemoAdmin = new("Administrator", "admin@shiftledger.local", "Admin#12345", Role.Admin);
    public static readonly DemoAccount DemoEmployee = new("Sam Carter", "sam@shiftledger.local", "Employee#123", Role.Employee);
    public static readonly DemoAccount DemoEmployee2 = new("Jordan Lee", "jordan@shiftledger.local", "Employee#123", Role.Employee);

    public static async Task SeedAsync(IServiceProvider services, bool seedDemoData = false)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();

        await SeedBootstrapAdminAsync(db, hasher, config);
        if (seedDemoData)
        {
            await SeedDemoDataAsync(db, hasher);
        }
    }

    // Creates a bootstrap Admin from config (BootstrapAdmin:Email/Password) if no users exist yet,
    // so the very first login has an account. No-op once any user exists.
    private static async Task SeedBootstrapAdminAsync(AppDbContext db, IPasswordHasher hasher, IConfiguration config)
    {
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

    // Idempotently ensures the fixed demo accounts exist. The two employees also get a pay profile
    // and a current pay rate so the P3 admin screens have data to show out of the box.
    private static async Task SeedDemoDataAsync(AppDbContext db, IPasswordHasher hasher)
    {
        await EnsureUserAsync(db, hasher, DemoAdmin);
        var sam = await EnsureUserAsync(db, hasher, DemoEmployee);
        var jordan = await EnsureUserAsync(db, hasher, DemoEmployee2);

        await EnsureProfileWithRateAsync(db, sam, RateType.Hourly, PayCycle.Weekly, 22.50m);
        await EnsureProfileWithRateAsync(db, jordan, RateType.Monthly, PayCycle.Monthly, 5200m);

        await db.SaveChangesAsync();
    }

    private static async Task<User> EnsureUserAsync(AppDbContext db, IPasswordHasher hasher, DemoAccount account)
    {
        var email = account.Email.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user is not null) return user;

        user = new User
        {
            FullName = account.FullName,
            Email = email,
            PasswordHash = hasher.Hash(account.Password),
            Role = account.Role,
        };
        db.Users.Add(user);
        return user;
    }

    private static async Task EnsureProfileWithRateAsync(
        AppDbContext db, User user, RateType rateType, PayCycle payCycle, decimal amount)
    {
        if (await db.EmployeeProfiles.AnyAsync(p => p.UserId == user.Id)) return;

        var profile = new EmployeeProfile { UserId = user.Id, RateType = rateType, PayCycle = payCycle };
        db.EmployeeProfiles.Add(profile);
        db.PayRates.Add(new PayRate
        {
            EmployeeProfileId = profile.Id,
            Amount = amount,
            EffectiveFrom = new DateOnly(2026, 1, 1),
            EffectiveTo = null,
        });
    }
}
