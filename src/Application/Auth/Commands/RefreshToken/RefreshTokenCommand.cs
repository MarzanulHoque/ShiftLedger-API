using MediatR;
using ShiftLedger.Application.Common.Messaging;
using ShiftLedger.Application.Common.Models;

namespace ShiftLedger.Application.Auth.Commands.RefreshToken;

public record RefreshTokenCommand(string RefreshToken) : IRequest<AuthResult>, ITransactionalRequest;
