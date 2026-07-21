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

    // Passwords are dev/demo only and intentionally known; they must be rotated before any
    // non-dev environment. Quote them directly when logging in to the local dev API.
    public static readonly DemoAccount DemoAdmin = new("Administrator", "admin@shiftledger.local", "Admin#12345", Role.Admin);
    public static readonly DemoAccount DemoEmployee = new("Sam Carter", "sam@shiftledger.local", "Employee#123", Role.Employee);
    public static readonly DemoAccount DemoEmployee2 = new("Jordan Lee", "jordan@shiftledger.local", "Employee#123", Role.Employee);
    public static readonly DemoAccount DemoEmployee3 = new("Alex Kim", "alex@shiftledger.local", "Employee#123", Role.Employee);

    public static async Task SeedAsync(IServiceProvider services, bool seedDemoData = false)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var timeProvider = scope.ServiceProvider.GetRequiredService<TimeProvider>();

        await SeedBootstrapAdminAsync(db, hasher, config);
        if (seedDemoData)
        {
            await SeedDemoDataAsync(db, hasher);
            await SeedDemoJobsAndBillsAsync(db, timeProvider);
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

    // Idempotently ensures the fixed demo accounts exist. The employees also get a pay profile,
    // a current pay rate, and a department so the P3 admin screens have data to show out of the box.
    private static async Task SeedDemoDataAsync(AppDbContext db, IPasswordHasher hasher)
    {
        var admin = await EnsureUserAsync(db, hasher, DemoAdmin);
        var sam = await EnsureUserAsync(db, hasher, DemoEmployee);
        var jordan = await EnsureUserAsync(db, hasher, DemoEmployee2);
        var alex = await EnsureUserAsync(db, hasher, DemoEmployee3);

        await EnsureProfileWithRateAsync(db, sam, RateType.Hourly, PayCycle.Weekly, 22.50m);
        await EnsureProfileWithRateAsync(db, jordan, RateType.Monthly, PayCycle.Monthly, 5200m);
        await EnsureProfileWithRateAsync(db, alex, RateType.Hourly, PayCycle.Weekly, 24.00m);

        var frontDesk = await EnsureDepartmentAsync(db, "Front Desk");
        var serviceBay = await EnsureDepartmentAsync(db, "Service Bay");
        admin.DepartmentId ??= frontDesk.Id;
        sam.DepartmentId ??= serviceBay.Id;
        jordan.DepartmentId ??= serviceBay.Id;
        alex.DepartmentId ??= serviceBay.Id;

        await db.SaveChangesAsync();
    }

    private static async Task<Department> EnsureDepartmentAsync(AppDbContext db, string name)
    {
        var department = await db.Departments.FirstOrDefaultAsync(d => d.Name == name);
        if (department is not null) return department;

        department = new Department { Name = name };
        db.Departments.Add(department);
        return department;
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

    private static readonly (string Title, JobPriority Priority)[] JobTemplates =
    [
        ("Flat repair", JobPriority.Low),
        ("Brake service", JobPriority.High),
        ("Full tune-up", JobPriority.Medium),
        ("Gear indexing", JobPriority.Low),
        ("Wheel truing", JobPriority.Medium),
        ("Chain replacement", JobPriority.Medium),
        ("Suspension service", JobPriority.High),
        ("New bike assembly", JobPriority.Medium),
        ("Disc brake bleed", JobPriority.High),
        ("Tire and tube replacement", JobPriority.Low),
    ];

    private static readonly string[] BikeModels =
    [
        "Trek FX 2", "Giant Escape 3", "Specialized Sirrus", "Cannondale Trail",
        "Scott Aspect", "Merida Big Nine", "Trek Domane", "Giant Defy",
        "Cube Kathmandu", "Bianchi Via Nirone",
    ];

    private static readonly string[] PartNames =
    [
        "Brake pads", "Brake cable", "Chain", "Cassette", "Inner tube",
        "Tire", "Handlebar tape", "Brake lever", "Derailleur hanger", "Spoke set",
    ];

    // Populates a realistic few weeks of shop history — spread across statuses, mechanics, and
    // paid/unpaid bills — so the dashboard charts and reports have a real distribution to draw
    // instead of a handful of manually-created rows. Guarded on ServiceJobs being empty so it
    // never re-seeds over jobs the shop has since created, edited, or deleted for real.
    private static async Task SeedDemoJobsAndBillsAsync(AppDbContext db, TimeProvider timeProvider)
    {
        if (await db.ServiceJobs.AnyAsync()) return;

        var mechanicIds = await db.Users.Where(u => u.Role == Role.Employee).Select(u => u.Id).ToListAsync();
        if (mechanicIds.Count == 0) return;

        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        var today = DateOnly.FromDateTime(nowUtc);
        var rng = new Random(20260101); // fixed seed: reproducible demo data

        var jobs = new List<ServiceJob>();
        var bills = new List<Bill>();
        var lineItems = new List<BillLineItem>();

        const int jobCount = 45;
        for (var i = 0; i < jobCount; i++)
        {
            var template = JobTemplates[rng.Next(JobTemplates.Length)];
            var daysAgo = rng.Next(0, 45);
            var receivedDate = today.AddDays(-daysAgo);
            var status = PickStatus(rng, daysAgo);

            var job = new ServiceJob
            {
                Title = template.Title,
                BikeModel = BikeModels[rng.Next(BikeModels.Length)],
                Priority = template.Priority,
                Status = status,
                // A brand-new intake is sometimes still unassigned; anything further along always has a mechanic.
                AssignedMechanicId = status == JobStatus.Received && rng.Next(4) == 0
                    ? null
                    : mechanicIds[rng.Next(mechanicIds.Count)],
                ReceivedDate = receivedDate,
                DueDate = receivedDate.AddDays(rng.Next(1, 7)),
            };
            jobs.Add(job);

            if (status is not (JobStatus.Completed or JobStatus.Delivered)) continue;

            // Delivered jobs are settled almost every time; a Completed job (bike still on-site)
            // is more often still awaiting payment.
            var isPaid = status == JobStatus.Delivered ? rng.Next(100) < 92 : rng.Next(100) < 45;
            var bill = new Bill { ServiceJobId = job.Id, IsPaid = isPaid };
            if (isPaid)
            {
                var paidAt = receivedDate.AddDays(rng.Next(0, 3))
                    .ToDateTime(TimeOnly.FromTimeSpan(TimeSpan.FromHours(9 + rng.Next(9))));
                bill.PaidAtUtc = paidAt > nowUtc ? nowUtc : paidAt;
            }
            bills.Add(bill);

            var laborHours = Math.Round(rng.Next(5, 30) / 10m, 1);
            lineItems.Add(new BillLineItem
            {
                BillId = bill.Id, Type = LineItemType.Labor, Description = "Labor", Quantity = laborHours, UnitPrice = 45m,
            });
            if (rng.Next(100) < 70)
            {
                lineItems.Add(new BillLineItem
                {
                    BillId = bill.Id,
                    Type = LineItemType.Part,
                    Description = PartNames[rng.Next(PartNames.Length)],
                    Quantity = rng.Next(1, 3),
                    UnitPrice = rng.Next(8, 60),
                });
            }
        }

        db.ServiceJobs.AddRange(jobs);
        db.Bills.AddRange(bills);
        db.BillLineItems.AddRange(lineItems);
        await db.SaveChangesAsync();
    }

    // A believable funnel: jobs received in the last day or two are still queued/in progress;
    // older intakes have mostly worked their way through to Completed/Delivered.
    private static JobStatus PickStatus(Random rng, int daysAgo) => daysAgo switch
    {
        <= 1 => rng.Next(100) < 70 ? JobStatus.Received : JobStatus.InProgress,
        <= 4 => rng.Next(4) switch { 0 => JobStatus.Received, 1 => JobStatus.InProgress, _ => JobStatus.Completed },
        <= 10 => rng.Next(100) < 60 ? JobStatus.Completed : JobStatus.Delivered,
        _ => rng.Next(100) < 15 ? JobStatus.Completed : JobStatus.Delivered,
    };
}
