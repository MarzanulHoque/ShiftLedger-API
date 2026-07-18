using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ShiftLedger.Application.Common.Exceptions;
using ShiftLedger.Application.Common.Interfaces;
using ShiftLedger.Domain.Entities;

namespace ShiftLedger.Application.Departments;

public record CreateDepartmentCommand(string Name) : IRequest<Guid>;

public class CreateDepartmentCommandValidator : AbstractValidator<CreateDepartmentCommand>
{
    public CreateDepartmentCommandValidator() => RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
}

public class CreateDepartmentCommandHandler(IAppDbContext db) : IRequestHandler<CreateDepartmentCommand, Guid>
{
    public async Task<Guid> Handle(CreateDepartmentCommand request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        if (await db.Departments.AnyAsync(d => d.Name == name, cancellationToken))
        {
            throw new BusinessRuleException("A department with this name already exists.");
        }

        var department = new Department { Name = name };
        db.Departments.Add(department);
        await db.SaveChangesAsync(cancellationToken);
        return department.Id;
    }
}
