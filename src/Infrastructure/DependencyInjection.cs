using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShiftLedger.Application.Common.Interfaces;
using ShiftLedger.Infrastructure.Persistence;

namespace ShiftLedger.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Connection string 'Default' is not configured.");

        // Explicit version so registration never opens a connection (unlike ServerVersion.AutoDetect).
        var serverVersion = new MySqlServerVersion(new Version(8, 0, 0));

        services.Configure<Application.Common.Options.JwtOptions>(
            configuration.GetSection(Application.Common.Options.JwtOptions.SectionName));

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IPasswordHasher, Security.PasswordHasher>();
        services.AddSingleton<IJwtTokenService, Security.JwtTokenService>();
        services.AddDbContext<AppDbContext>(options => options.UseMySql(connectionString, serverVersion));
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        return services;
    }
}
