using MediatR;
using Microsoft.EntityFrameworkCore;
using ShiftLedger.Application.Common.Interfaces;

namespace ShiftLedger.Application.OrgSettings.Queries.GetOrgSettings;

public record OrgSettingsDto(DayOfWeek WeekStartDay, string CurrencyCode, decimal OvertimeMultiplier);

public record GetOrgSettingsQuery : IRequest<OrgSettingsDto>;

public class GetOrgSettingsQueryHandler(IAppDbContext db) : IRequestHandler<GetOrgSettingsQuery, OrgSettingsDto>
{
    public async Task<OrgSettingsDto> Handle(GetOrgSettingsQuery request, CancellationToken cancellationToken)
    {
        var settings = await db.OrgSettings.AsNoTracking().FirstAsync(cancellationToken);
        return new OrgSettingsDto(settings.WeekStartDay, settings.CurrencyCode, settings.OvertimeMultiplier);
    }
}
