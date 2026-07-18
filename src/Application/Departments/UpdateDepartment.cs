using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ShiftLedger.Application.Common.Exceptions;
using ShiftLedger.Application.Common.Interfaces;

namespace ShiftLedger.Application.Departments;

public record UpdateDepartmentCommand(Guid Id, string Name) : IRequest;

public class UpdateDepartmentCommandValidator : AbstractValidator<UpdateDepartmentCommand>
{
    public UpdateDepartmentCommandValidator() => RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
}

public class UpdateDepartmentCommandHandler(IAppDbContext db) : IRequestHandler<UpdateDepartmentCommand>
{
    public async Task Handle(UpdateDepartmentCommand request, CancellationToken cancellationToken)
    {
        var department = await db.Departments.FirstOrDefaultAsync(d => d.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Department not found.");

        department.Name = request.Name.Trim();
        await db.SaveChangesAsync(cancellationToken);
    }
}
