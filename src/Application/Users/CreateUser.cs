using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ShiftLedger.Application.Common.Exceptions;
using ShiftLedger.Application.Common.Interfaces;
using ShiftLedger.Domain.Entities;
using ShiftLedger.Domain.Enums;

namespace ShiftLedger.Application.Users;

// Admin-provisioned account creation (no self-registration). Rule R1.
public record CreateUserCommand(string FullName, string Email, string Password, Role Role, Guid? DepartmentId)
    : IRequest<Guid>;

public class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserCommandValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8);
    }
}

public class CreateUserCommandHandler(IAppDbContext db, IPasswordHasher hasher, ICurrentUser currentUser, IDepartmentScope departmentScope)
    : IRequestHandler<CreateUserCommand, Guid>
{
    public async Task<Guid> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        // Rule RB1: the single org-wide Super Admin is seeded once at startup, never provisioned via this endpoint.
        if (request.Role == Role.SuperAdmin)
        {
            throw new BusinessRuleException("A Super Admin account cannot be created here.");
        }

        if (request.DepartmentId is not { } departmentId)
        {
            throw new BusinessRuleException("A department is required for this role.");
        }

        // Rule RB5: a DepartmentAdmin may only provision Employees into their own department; SuperAdmin bypasses (RB0).
        if (!currentUser.IsSuperAdmin)
        {
            if (request.Role != Role.Employee)
            {
                throw new ForbiddenException();
            }
            departmentScope.EnsureAccess(departmentId);
        }

        var email = request.Email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(u => u.Email == email, cancellationToken))
        {
            throw new BusinessRuleException("A user with this email already exists.");
        }

        var user = new User
        {
            FullName = request.FullName.Trim(),
            Email = email,
            PasswordHash = hasher.Hash(request.Password),
            Role = request.Role,
            DepartmentId = request.DepartmentId,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);
        return user.Id;
    }
}
