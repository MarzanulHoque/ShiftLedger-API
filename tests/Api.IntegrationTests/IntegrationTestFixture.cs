using Microsoft.EntityFrameworkCore;
using ShiftLedger.Infrastructure.Persistence;
using Xunit;

namespace ShiftLedger.Api.IntegrationTests;

// Runs integration tests against a real MySQL, using a dedicated test schema that is dropped and
// re-migrated per run. Connection comes from the ConnectionStrings__Default env var (a *_test schema).
public class IntegrationTestFixture : IAsyncLifetime
{
    private readonly DbContextOptions<AppDbContext> _options;

    public IntegrationTestFixture()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Default");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Integration tests need a MySQL connection in ConnectionStrings__Default (point it at a dedicated *_test schema).");
        }

        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 0)))
            .Options;
    }

    public AppDbContext CreateContext() => new(_options, TimeProvider.System);

    public async Task InitializeAsync()
    {
        await using var ctx = CreateContext();
        await ctx.Database.EnsureDeletedAsync();
        await ctx.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await using var ctx = CreateContext();
        await ctx.Database.EnsureDeletedAsync();
    }
}
