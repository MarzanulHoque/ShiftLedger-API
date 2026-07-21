using MediatR;
using Microsoft.EntityFrameworkCore;
using ShiftLedger.Application.Common.Interfaces;
using ShiftLedger.Application.Common.Models;
using ShiftLedger.Domain.Enums;

namespace ShiftLedger.Application.Jobs;

public record JobDto(
    Guid Id, int JobNumber, Guid DepartmentId, string Title, string? Description, string BikeModel, JobStatus Status,
    JobPriority Priority, Guid? AssignedMechanicId, DateOnly ReceivedDate, DateOnly? DueDate);

public record GetJobsQuery(JobStatus? Status, Guid? MechanicId, int? Page = null, int? PageSize = null)
    : IRequest<PagedResult<JobDto>>;

public class GetJobsQueryHandler(IAppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<GetJobsQuery, PagedResult<JobDto>>
{
    public async Task<PagedResult<JobDto>> Handle(GetJobsQuery request, CancellationToken cancellationToken)
    {
        // Soft-deleted jobs are excluded by the global query filter.
        var query = db.ServiceJobs.AsNoTracking();

        // Rule R2: a mechanic only ever sees the jobs assigned to them. Admins may filter by mechanic.
        if (!currentUser.IsAdmin)
        {
            query = query.Where(j => j.AssignedMechanicId == currentUser.UserId);
        }
        else if (request.MechanicId is { } mechanicId)
        {
            query = query.Where(j => j.AssignedMechanicId == mechanicId);
        }

        if (request.Status is { } status)
        {
            query = query.Where(j => j.Status == status);
        }

        return await query
            .OrderByDescending(j => j.ReceivedDate)
            .ThenBy(j => j.Title)
            .Select(j => new JobDto(
                j.Id, j.JobNumber, j.DepartmentId, j.Title, j.Description, j.BikeModel, j.Status, j.Priority,
                j.AssignedMechanicId, j.ReceivedDate, j.DueDate))
            .ToPagedResultAsync(request.Page, request.PageSize, cancellationToken);
    }
}
