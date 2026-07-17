using MediatR;
using ShiftLedger.Application.Common.Interfaces;
using ShiftLedger.Application.Common.Messaging;

namespace ShiftLedger.Application.Common.Behaviors;

// Rule C4: wraps an ITransactionalRequest command in one DB transaction — commit on success,
// rollback on failure (the un-committed transaction is rolled back on dispose).
public class TransactionBehavior<TRequest, TResponse>(IAppDbContext db)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (request is not ITransactionalRequest)
        {
            return await next();
        }

        await using var transaction = await db.BeginTransactionAsync(cancellationToken);
        var response = await next();
        await transaction.CommitAsync(cancellationToken);
        return response;
    }
}
