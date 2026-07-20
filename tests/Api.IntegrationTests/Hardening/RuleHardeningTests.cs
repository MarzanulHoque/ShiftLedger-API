using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ShiftLedger.Domain.Entities;
using Xunit;

namespace ShiftLedger.Api.IntegrationTests.Hardening;

[Collection("Database")]
public class RuleHardeningTests(IntegrationTestFixture fixture)
{
    // Rule C4: a multi-step save commits fully or not at all. Two bills for one job in a single
    // SaveChanges — the second violates the unique index, and the first must roll back with it.
    [Fact]
    public async Task MultiStepSave_FailingStep_RollsBackFully_C4()
    {
        Guid jobId;
        await using (var setup = fixture.CreateContext())
        {
            var job = new ServiceJob { Title = "C4 job", BikeModel = "Test bike", ReceivedDate = new DateOnly(2026, 7, 19) };
            setup.ServiceJobs.Add(job);
            await setup.SaveChangesAsync();
            jobId = job.Id;
        }

        await using (var ctx = fixture.CreateContext())
        {
            ctx.Bills.Add(new Bill { ServiceJobId = jobId });
            ctx.Bills.Add(new Bill { ServiceJobId = jobId }); // violates the unique ServiceJobId index
            var act = () => ctx.SaveChangesAsync();
            await act.Should().ThrowAsync<DbUpdateException>();
        }

        await using var verify = fixture.CreateContext();
        (await verify.Bills.CountAsync(b => b.ServiceJobId == jobId)).Should().Be(0,
            "the whole save must roll back — no half-committed state (C4)");
    }

    // docs/09: secret material never lands in audit JSON.
    [Fact]
    public async Task AuditRows_ExcludeSecretHashes()
    {
        var email = $"audit-secret-{Guid.NewGuid():N}@test.local";
        await using (var setup = fixture.CreateContext())
        {
            setup.Users.Add(new User
            {
                FullName = "Audit Secret",
                Email = email,
                PasswordHash = "hash-that-must-not-be-audited",
                Role = Domain.Enums.Role.Employee,
            });
            await setup.SaveChangesAsync();
        }

        await using var verify = fixture.CreateContext();
        var user = await verify.Users.FirstAsync(u => u.Email == email);
        var audit = await verify.AuditLogs.FirstAsync(a => a.EntityName == nameof(User) && a.EntityId == user.Id.ToString());
        audit.NewValuesJson.Should().NotContain("PasswordHash").And.NotContain("hash-that-must-not-be-audited");
    }

    // Rule A2: AuditLog rows can never be updated or deleted — the context refuses the save.
    [Fact]
    public async Task AuditLog_UpdateOrDelete_IsRejected_A2()
    {
        await using (var setup = fixture.CreateContext())
        {
            setup.Departments.Add(new Department { Name = $"A2 dept {Guid.NewGuid():N}" });
            await setup.SaveChangesAsync(); // writes a 'Created' audit row
        }

        await using (var ctx = fixture.CreateContext())
        {
            var row = await ctx.AuditLogs.FirstAsync();
            row.Action = "Tampered";
            var update = () => ctx.SaveChangesAsync();
            await update.Should().ThrowAsync<InvalidOperationException>().WithMessage("*append-only*");
        }

        await using (var ctx = fixture.CreateContext())
        {
            var row = await ctx.AuditLogs.FirstAsync();
            ctx.AuditLogs.Remove(row);
            var delete = () => ctx.SaveChangesAsync();
            await delete.Should().ThrowAsync<InvalidOperationException>().WithMessage("*append-only*");
        }
    }
}
