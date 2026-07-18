using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ShiftLedger.Application.Common.Exceptions;
using ShiftLedger.Application.Common.Interfaces;
using ShiftLedger.Domain.Entities;

namespace ShiftLedger.Application.PayRates;

// Add a new effective-dated rate for a user's profile. History is append-only (Rule P3):
// the previously-current rate is closed (its EffectiveTo set to the day before the new one),
// never edited. The new rate becomes the current (open) rate.
// Request body for POST /users/{id}/pay-rates — the user comes from the route.
public record AddPayRateRequest(decimal Amount, DateOnly EffectiveFrom);

public record AddPayRateCommand(Guid UserId, decimal Amount, DateOnly EffectiveFrom) : IRequest<Guid>;

public class AddPayRateCommandValidator : AbstractValidator<AddPayRateCommand>
{
    public AddPayRateCommandValidator() => RuleFor(x => x.Amount).GreaterThan(0);
}

public class AddPayRateCommandHandler(IAppDbContext db) : IRequestHandler<AddPayRateCommand, Guid>
{
    public async Task<Guid> Handle(AddPayRateCommand request, CancellationToken cancellationToken)
    {
        var profile = await db.EmployeeProfiles.FirstOrDefaultAsync(p => p.UserId == request.UserId, cancellationToken)
            ?? throw new NotFoundException("This user has no employee profile; create one before adding pay rates.");

        // The most recent existing rate. A new rate must start strictly after it so the
        // history stays ordered and non-overlapping.
        var latest = await db.PayRates
            .Where(r => r.EmployeeProfileId == profile.Id)
            .OrderByDescending(r => r.EffectiveFrom)
            .FirstOrDefaultAsync(cancellationToken);

        if (latest is not null && request.EffectiveFrom <= latest.EffectiveFrom)
        {
            throw new BusinessRuleException("A new pay rate must take effect after the current rate's effective date.");
        }

        // Close the currently-open rate the day before the new one takes effect.
        if (latest is { EffectiveTo: null })
        {
            latest.EffectiveTo = request.EffectiveFrom.AddDays(-1);
        }

        var rate = new PayRate
        {
            EmployeeProfileId = profile.Id,
            Amount = request.Amount,
            EffectiveFrom = request.EffectiveFrom,
            EffectiveTo = null,
        };
        db.PayRates.Add(rate);
        await db.SaveChangesAsync(cancellationToken);
        return rate.Id;
    }
}
