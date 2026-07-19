using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ShiftLedger.Application.Common.Exceptions;
using ShiftLedger.Application.Common.Interfaces;
using ShiftLedger.Domain.Entities;
using ShiftLedger.Domain.Enums;

namespace ShiftLedger.Application.Jobs;

// Intake of a new service job (Admin action). BikeModel is free text in v1 (no Customer/Bike entity).
public record CreateJobCommand(
    string Title,
    string? Description,
    string BikeModel,
    JobPriority? Priority,
    Guid? AssignedMechanicId,
    DateOnly? ReceivedDate,
    DateOnly? DueDate) : IRequest<Guid>;

public class CreateJobCommandValidator : AbstractValidator<CreateJobCommand>
{
    public CreateJobCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.BikeModel).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Description).MaximumLength(2000);
    }
}

public class CreateJobCommandHandler(IAppDbContext db, TimeProvider timeProvider, INotifier notifier)
    : IRequestHandler<CreateJobCommand, Guid>
{
    public async Task<Guid> Handle(CreateJobCommand request, CancellationToken cancellationToken)
    {
        if (request.AssignedMechanicId is { } mechanicId)
        {
            await EnsureMechanicAsync(db, mechanicId, cancellationToken);
        }

        var job = new ServiceJob
        {
            Title = request.Title.Trim(),
            Description = request.Description?.Trim(),
            BikeModel = request.BikeModel.Trim(),
            Priority = request.Priority ?? JobPriority.Medium,
            Status = JobStatus.Received,
            AssignedMechanicId = request.AssignedMechanicId,
            ReceivedDate = request.ReceivedDate ?? DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime),
            DueDate = request.DueDate,
        };
        db.ServiceJobs.Add(job);
        await db.SaveChangesAsync(cancellationToken);

        if (job.AssignedMechanicId is { } assignee)
        {
            await notifier.NotifyAsync(assignee, "JobAssigned", $"New job assigned to you: {job.Title}", cancellationToken);
        }
        return job.Id;
    }

    // Rule J2: a job's assignee must be an existing user with the Employee (mechanic) role.
    internal static async Task EnsureMechanicAsync(IAppDbContext db, Guid mechanicId, CancellationToken ct)
    {
        var role = await db.Users.Where(u => u.Id == mechanicId)
            .Select(u => (Role?)u.Role).FirstOrDefaultAsync(ct);
        if (role is null) throw new NotFoundException("Assigned mechanic not found.");
        if (role != Role.Employee) throw new BusinessRuleException("Only an Employee (mechanic) can be assigned to a job.");
    }
}
