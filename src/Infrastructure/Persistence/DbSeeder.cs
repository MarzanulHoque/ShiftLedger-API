using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShiftLedger.Application.Common.Interfaces;
using ShiftLedger.Domain.Entities;
using ShiftLedger.Domain.Enums;
using ShiftLedger.Infrastructure.Persistence.Configurations;

namespace ShiftLedger.Infrastructure.Persistence;

// v2 restructure: seeds the two phase-1 departments' full staff tree (SuperAdmin + a
// DepartmentAdmin and two Employees per department) instead of a single flat Admin/Employee set.
// Departments themselves are migration-seeded (fixed IDs — DepartmentConfiguration.HasData), not
// created here, so they exist even when seedDemoData is off.
public static class DbSeeder
{
    // A fixed demo account (Development only) backing a quick-login button in the client.
    public sealed record DemoAccount(string FullName, string Email, string Password, Role Role, Guid? DepartmentId);

    // Passwords are dev/demo only and intentionally known; they must be rotated before any
    // non-dev environment. Quote them directly when logging in to the local dev API.
    public static readonly DemoAccount DemoSuperAdmin =
        new("Administrator", "admin@shiftledger.local", "Admin#12345", Role.SuperAdmin, DepartmentId: null);

    public static readonly DemoAccount DemoMechanicsAdmin =
        new("Morgan Reyes", "morgan@shiftledger.local", "DeptAdmin#123", Role.DepartmentAdmin, DepartmentConfiguration.MechanicsId);
    public static readonly DemoAccount DemoMechanicsEmployee1 =
        new("Sam Carter", "sam@shiftledger.local", "Employee#123", Role.Employee, DepartmentConfiguration.MechanicsId);
    public static readonly DemoAccount DemoMechanicsEmployee2 =
        new("Jordan Lee", "jordan@shiftledger.local", "Employee#123", Role.Employee, DepartmentConfiguration.MechanicsId);

    public static readonly DemoAccount DemoBikeWashAdmin =
        new("Priya Shah", "priya@shiftledger.local", "DeptAdmin#123", Role.DepartmentAdmin, DepartmentConfiguration.BikeWashId);
    public static readonly DemoAccount DemoBikeWashEmployee1 =
        new("Alex Kim", "alex@shiftledger.local", "Employee#123", Role.Employee, DepartmentConfiguration.BikeWashId);
    public static readonly DemoAccount DemoBikeWashEmployee2 =
        new("Chris Park", "chris@shiftledger.local", "Employee#123", Role.Employee, DepartmentConfiguration.BikeWashId);

    public static async Task SeedAsync(IServiceProvider services, bool seedDemoData = false)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var timeProvider = scope.ServiceProvider.GetRequiredService<TimeProvider>();

        await SeedBootstrapSuperAdminAsync(db, hasher, config);
        if (seedDemoData)
        {
            await SeedDemoDataAsync(db, hasher);
            await SeedDemoJobsAndBillsAsync(db, timeProvider);
        }
    }

    // Creates a bootstrap SuperAdmin from config (BootstrapAdmin:Email/Password) if no users exist
    // yet, so the very first login has an account (Rule RB1). No-op once any user exists.
    private static async Task SeedBootstrapSuperAdminAsync(AppDbContext db, IPasswordHasher hasher, IConfiguration config)
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
            Role = Role.SuperAdmin,
        });
        await db.SaveChangesAsync();
    }

    // Idempotently ensures the fixed demo accounts exist: one SuperAdmin plus a DepartmentAdmin
    // and two Employees per phase-1 department. Employees also get a pay profile + current pay
    // rate so the (parked, unsurfaced) payroll tables have realistic data ready for v3.
    private static async Task SeedDemoDataAsync(AppDbContext db, IPasswordHasher hasher)
    {
        await EnsureUserAsync(db, hasher, DemoSuperAdmin);
        await EnsureUserAsync(db, hasher, DemoMechanicsAdmin);
        var sam = await EnsureUserAsync(db, hasher, DemoMechanicsEmployee1);
        var jordan = await EnsureUserAsync(db, hasher, DemoMechanicsEmployee2);
        await EnsureUserAsync(db, hasher, DemoBikeWashAdmin);
        var alex = await EnsureUserAsync(db, hasher, DemoBikeWashEmployee1);
        var chris = await EnsureUserAsync(db, hasher, DemoBikeWashEmployee2);

        await EnsureProfileWithRateAsync(db, sam, RateType.Hourly, PayCycle.Weekly, 22.50m);
        await EnsureProfileWithRateAsync(db, jordan, RateType.Monthly, PayCycle.Monthly, 5200m);
        await EnsureProfileWithRateAsync(db, alex, RateType.Hourly, PayCycle.Weekly, 18.00m);
        await EnsureProfileWithRateAsync(db, chris, RateType.Hourly, PayCycle.Weekly, 17.50m);

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
            DepartmentId = account.DepartmentId,
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

    private static readonly (string Title, JobPriority Priority)[] MechanicsJobTemplates =
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

    private static readonly (string Title, JobPriority Priority)[] BikeWashJobTemplates =
    [
        ("Standard wash", JobPriority.Low),
        ("Deep clean + degrease", JobPriority.Medium),
        ("Wash + chain lube", JobPriority.Low),
        ("Frame detail & polish", JobPriority.Medium),
        ("Wash + drivetrain clean", JobPriority.Medium),
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

    // Populates a realistic few weeks of history for each department - spread across statuses,
    // employees, and paid/unpaid bills - so the dashboards/reports have a real distribution to
    // draw instead of a handful of manually-created rows. Guarded on ServiceJobs being empty so it
    // never re-seeds over jobs a shop has since created, edited, or deleted for real.
    private static async Task SeedDemoJobsAndBillsAsync(AppDbContext db, TimeProvider timeProvider)
    {
        if (await db.ServiceJobs.AnyAsync()) return;

        var rng = new Random(20260101); // fixed seed: reproducible demo data across both departments

        await SeedDepartmentJobsAndBillsAsync(
            db, timeProvider, rng, DepartmentConfiguration.MechanicsId, MechanicsJobTemplates,
            laborRate: 45m, jobCount: 25);
        await SeedDepartmentJobsAndBillsAsync(
            db, timeProvider, rng, DepartmentConfiguration.BikeWashId, BikeWashJobTemplates,
            laborRate: 18m, jobCount: 20);
    }

    private static async Task SeedDepartmentJobsAndBillsAsync(
        AppDbContext db, TimeProvider timeProvider, Random rng, Guid departmentId,
        (string Title, JobPriority Priority)[] jobTemplates, decimal laborRate, int jobCount)
    {
        var employeeIds = await db.Users
            .Where(u => u.Role == Role.Employee && u.DepartmentId == departmentId)
            .Select(u => u.Id).ToListAsync();
        if (employeeIds.Count == 0) return;

        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        var today = DateOnly.FromDateTime(nowUtc);

        var jobs = new List<ServiceJob>();
        var bills = new List<Bill>();
        var lineItems = new List<BillLineItem>();

        for (var i = 0; i < jobCount; i++)
        {
            var template = jobTemplates[rng.Next(jobTemplates.Length)];
            var daysAgo = rng.Next(0, 45);
            var receivedDate = today.AddDays(-daysAgo);
            var status = PickStatus(rng, daysAgo);

            var job = new ServiceJob
            {
                DepartmentId = departmentId,
                Title = template.Title,
                BikeModel = BikeModels[rng.Next(BikeModels.Length)],
                Priority = template.Priority,
                Status = status,
                // A brand-new intake is sometimes still unassigned; anything further along always has an assignee.
                AssignedMechanicId = status == JobStatus.Received && rng.Next(4) == 0
                    ? null
                    : employeeIds[rng.Next(employeeIds.Count)],
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
                BillId = bill.Id, Type = LineItemType.Labor, Description = "Labor", Quantity = laborHours, UnitPrice = laborRate,
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
