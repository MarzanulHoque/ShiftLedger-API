using MediatR;
using Microsoft.EntityFrameworkCore;
using ShiftLedger.Application.Common.Exceptions;
using ShiftLedger.Application.Common.Interfaces;

namespace ShiftLedger.Application.PayRates;

public record PayRateDto(Guid Id, decimal Amount, DateOnly EffectiveFrom, DateOnly? EffectiveTo);

public record GetPayRatesQuery(Guid UserId) : IRequest<IReadOnlyList<PayRateDto>>;

public class GetPayRatesQueryHandler(IAppDbContext db) : IRequestHandler<GetPayRatesQuery, IReadOnlyList<PayRateDto>>
{
    public async Task<IReadOnlyList<PayRateDto>> Handle(GetPayRatesQuery request, CancellationToken cancellationToken)
    {
        var profileId = await db.EmployeeProfiles
            .Where(p => p.UserId == request.UserId)
            .Select(p => (Guid?)p.Id)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("This user has no employee profile.");

        // Newest first so the current (open) rate leads the history.
        return await db.PayRates.AsNoTracking()
            .Where(r => r.EmployeeProfileId == profileId)
            .OrderByDescending(r => r.EffectiveFrom)
            .Select(r => new PayRateDto(r.Id, r.Amount, r.EffectiveFrom, r.EffectiveTo))
            .ToListAsync(cancellationToken);
    }
}
