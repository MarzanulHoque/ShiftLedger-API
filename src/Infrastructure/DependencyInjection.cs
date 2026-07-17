using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ShiftLedger.Infrastructure;

/// <summary>
/// Composition root for the Infrastructure layer. The API calls <see cref="AddInfrastructure"/>
/// to register persistence (EF Core / Pomelo MySQL) and external services.
/// <para>
/// Placeholder for phase P0 — the scaffold builds and boots without a database.
/// Persistence (AppDbContext, SaveChanges interceptors, repositories) is wired here in phase P1
/// (see plans/Implementation_Plan.md). The signature already takes <see cref="IConfiguration"/>
/// so P1 can read the connection string without changing Program.cs.
/// </para>
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // P1: services.AddDbContext<AppDbContext>(...) using the "Default" connection string.
        return services;
    }
}
