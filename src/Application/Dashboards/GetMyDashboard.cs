using MediatR;
using Microsoft.EntityFrameworkCore;
using ShiftLedger.Application.Common.Exceptions;
using ShiftLedger.Application.Common.Interfaces;
using ShiftLedger.Application.Jobs;
using ShiftLedger.Domain.Enums;

namespace ShiftLedger.Application.Dashboards;

// A mechanic's day view: their queue by status plus the open jobs themselves (Rule R2 — own data only).
public record MyDashboardDto(IReadOnlyList<StatusCountDto> MyJobsByStatus, IReadOnlyList<JobDto> MyOpenJobs);

public record GetMyDashboardQuery : IRequest<MyDashboardDto>;

public class GetMyDashboardQueryHandler(IAppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<GetMyDashboardQuery, MyDashboardDto>
{
    public async Task<MyDashboardDto> Handle(GetMyDashboardQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new ForbiddenException();

        var mine = db.ServiceJobs.AsNoTracking().Where(j => j.AssignedMechanicId == userId);

        var byStatus = await mine
            .GroupBy(j => j.Status)
            .Select(g => new StatusCountDto(g.Key, g.Count()))
            .ToListAsync(cancellationToken);

        var openJobs = await mine
            .Where(j => j.Status != JobStatus.Delivered)
            .OrderBy(j => j.DueDate == null)
            .ThenBy(j => j.DueDate)
            .ThenByDescending(j => j.ReceivedDate)
            .Select(j => new JobDto(
                j.Id, j.Title, j.Description, j.BikeModel, j.Status, j.Priority,
                j.AssignedMechanicId, j.ReceivedDate, j.DueDate))
            .ToListAsync(cancellationToken);

        return new MyDashboardDto(byStatus, openJobs);
    }
}
