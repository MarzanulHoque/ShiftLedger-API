using FluentValidation;
using MediatR;
using ShiftLedger.Application.Common.Messaging;
using ShiftLedger.Application.Common.Models;

namespace ShiftLedger.Application.Auth.Commands.Login;

public record LoginCommand(string Email, string Password) : IRequest<AuthResult>, ITransactionalRequest;

public class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty();
    }
}
