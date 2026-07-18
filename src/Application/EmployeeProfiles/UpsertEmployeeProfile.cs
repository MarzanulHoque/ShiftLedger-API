using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ShiftLedger.Application.Common.Exceptions;
using ShiftLedger.Application.Common.Interfaces;
using ShiftLedger.Domain.Entities;
using ShiftLedger.Domain.Enums;

namespace ShiftLedger.Application.EmployeeProfiles;

// Request body for PUT /users/{id}/employee-profile — the user comes from the route.
public record UpsertEmployeeProfileRequest(RateType RateType, PayCycle PayCycle);

// Create-or-update the pay profile for a user (PUT is idempotent). A user has at most one profile.
public record UpsertEmployeeProfileCommand(Guid UserId, RateType RateType, PayCycle PayCycle)
    : IRequest<EmployeeProfileDto>;

public class UpsertEmployeeProfileCommandValidator : AbstractValidator<UpsertEmployeeProfileCommand>
{
    public UpsertEmployeeProfileCommandValidator()
    {
        RuleFor(x => x.RateType).IsInEnum();
        RuleFor(x => x.PayCycle).IsInEnum();
    }
}

public class UpsertEmployeeProfileCommandHandler(IAppDbContext db)
    : IRequestHandler<UpsertEmployeeProfileCommand, EmployeeProfileDto>
{
    public async Task<EmployeeProfileDto> Handle(UpsertEmployeeProfileCommand request, CancellationToken cancellationToken)
    {
        if (!await db.Users.AnyAsync(u => u.Id == request.UserId, cancellationToken))
        {
            throw new NotFoundException("User not found.");
        }

        var profile = await db.EmployeeProfiles.FirstOrDefaultAsync(p => p.UserId == request.UserId, cancellationToken);
        if (profile is null)
        {
            profile = new EmployeeProfile { UserId = request.UserId };
            db.EmployeeProfiles.Add(profile);
        }

        profile.RateType = request.RateType;
        profile.PayCycle = request.PayCycle;
        await db.SaveChangesAsync(cancellationToken);

        return new EmployeeProfileDto(profile.Id, profile.UserId, profile.RateType, profile.PayCycle);
    }
}
