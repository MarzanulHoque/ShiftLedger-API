using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ShiftLedger.Application.Common.Exceptions;
using ShiftLedger.Application.Common.Interfaces;
using ShiftLedger.Domain.Enums;

namespace ShiftLedger.Application.Jobs;

// Edit a job's descriptive fields (Admin). Status changes and assignment have their own commands.
public record UpdateJobCommand(
    Guid Id, string Title, string? Description, string BikeModel, JobPriority Priority, DateOnly? DueDate)
    : IRequest;

public class UpdateJobCommandValidator : AbstractValidator<UpdateJobCommand>
{
    public UpdateJobCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.BikeModel).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Description).MaximumLength(2000);
    }
}

public class UpdateJobCommandHandler(IAppDbContext db) : IRequestHandler<UpdateJobCommand>
{
    public async Task Handle(UpdateJobCommand request, CancellationToken cancellationToken)
    {
        var job = await db.ServiceJobs.FirstOrDefaultAsync(j => j.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Service job not found.");

        job.Title = request.Title.Trim();
        job.Description = request.Description?.Trim();
        job.BikeModel = request.BikeModel.Trim();
        job.Priority = request.Priority;
        job.DueDate = request.DueDate;
        await db.SaveChangesAsync(cancellationToken);
    }
}
