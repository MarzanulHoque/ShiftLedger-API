using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ShiftLedger.Api.IntegrationTests.Persistence;

[Collection("Database")]
public class AppDbContextTests(IntegrationTestFixture fixture)
{
    // Rule C1: a stale RowVersion loses the race and fails loudly.
    [Fact]
    public async Task Update_WithStaleRowVersion_ThrowsConcurrency_C1()
    {
        await using var ctxA = fixture.CreateContext();
        await using var ctxB = fixture.CreateContext();

        var a = await ctxA.OrgSettings.SingleAsync();
        var b = await ctxB.OrgSettings.SingleAsync();

        a.OvertimeMultiplier += 1m; // a real change relative to the loaded value
        await ctxA.SaveChangesAsync();

        b.OvertimeMultiplier += 2m; // b's RowVersion is now stale
        var save = async () => await ctxB.SaveChangesAsync();

        await save.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }

    // Rules A1/A2: an update writes an append-only AuditLog row.
    [Fact]
    public async Task Update_WritesAuditLogRow_A1()
    {
        await using var ctx = fixture.CreateContext();

        var settings = await ctx.OrgSettings.SingleAsync();
        settings.OvertimeMultiplier += 0.1m;
        await ctx.SaveChangesAsync();

        var audits = await ctx.AuditLogs
            .Where(x => x.EntityName == "OrgSettings" && x.Action == "Modified")
            .ToListAsync();

        audits.Should().NotBeEmpty();
    }

    // Rule C1: the concurrency token changes on every update.
    [Fact]
    public async Task Update_RegeneratesRowVersion_C1()
    {
        await using var ctx = fixture.CreateContext();

        var settings = await ctx.OrgSettings.SingleAsync();
        var before = settings.RowVersion;

        settings.CurrencyCode = settings.CurrencyCode == "USD" ? "EUR" : "USD";
        await ctx.SaveChangesAsync();

        settings.RowVersion.Should().NotBe(before);
    }
}
