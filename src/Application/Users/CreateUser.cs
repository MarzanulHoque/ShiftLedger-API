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

public class CreateUserCommandHandler(IAppDbContext db, IPasswordHasher hasher)
    : IRequestHandler<CreateUserCommand, Guid>
{
    public async Task<Guid> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
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
