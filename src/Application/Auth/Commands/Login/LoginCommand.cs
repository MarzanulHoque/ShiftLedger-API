using FluentValidation;
using MediatR;
using ShiftLedger.Application.Common.Models;

namespace ShiftLedger.Application.Auth.Commands.Login;

// Not transactional: on failed login we persist the failed-attempt counter, which an outer
// transaction would roll back when the handler throws.
public record LoginCommand(string Email, string Password) : IRequest<AuthResult>;

public class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty();
    }
}
