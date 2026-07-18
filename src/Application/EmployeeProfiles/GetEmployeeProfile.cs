using MediatR;
using Microsoft.EntityFrameworkCore;
using ShiftLedger.Application.Common.Exceptions;
using ShiftLedger.Application.Common.Interfaces;
using ShiftLedger.Domain.Enums;

namespace ShiftLedger.Application.EmployeeProfiles;

public record EmployeeProfileDto(Guid Id, Guid UserId, RateType RateType, PayCycle PayCycle);

public record GetEmployeeProfileQuery(Guid UserId) : IRequest<EmployeeProfileDto>;

public class GetEmployeeProfileQueryHandler(IAppDbContext db) : IRequestHandler<GetEmployeeProfileQuery, EmployeeProfileDto>
{
    public async Task<EmployeeProfileDto> Handle(GetEmployeeProfileQuery request, CancellationToken cancellationToken)
    {
        var profile = await db.EmployeeProfiles.AsNoTracking()
            .Where(p => p.UserId == request.UserId)
            .Select(p => new EmployeeProfileDto(p.Id, p.UserId, p.RateType, p.PayCycle))
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("This user has no employee profile.");

        return profile;
    }
}
