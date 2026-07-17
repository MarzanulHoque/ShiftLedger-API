using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace ShiftLedger.Application;

/// <summary>
/// Composition root for the Application layer. The API calls <see cref="AddApplication"/>
/// so all use-case wiring (MediatR handlers + FluentValidation validators) lives in one
/// discoverable place per layer, per Clean Architecture (see docs/02_Architecture.md).
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var applicationAssembly = Assembly.GetExecutingAssembly();

        // Register every MediatR command/query handler declared in this assembly.
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(applicationAssembly));

        // Register every FluentValidation validator declared in this assembly.
        services.AddValidatorsFromAssembly(applicationAssembly);

        return services;
    }
}
