using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ShiftLedger.Application.Common.Exceptions;
using ShiftLedger.Application.Common.Interfaces;
using ShiftLedger.Domain.Enums;

namespace ShiftLedger.Application.Bills;

public record UpdateLineItemCommand(
    Guid BillId, Guid LineId, LineItemType Type, string Description, decimal Quantity, decimal UnitPrice) : IRequest;

// Rule B5: same bounds as on add.
public class UpdateLineItemCommandValidator : AbstractValidator<UpdateLineItemCommand>
{
    public UpdateLineItemCommandValidator()
    {
        RuleFor(x => x.Description).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Quantity).GreaterThan(0);
        RuleFor(x => x.UnitPrice).GreaterThanOrEqualTo(0);
    }
}

public class UpdateLineItemCommandHandler(IAppDbContext db, ICurrentUser currentUser) : IRequestHandler<UpdateLineItemCommand>
{
    public async Task Handle(UpdateLineItemCommand request, CancellationToken cancellationToken)
    {
        var bill = await db.Bills.AsNoTracking().FirstOrDefaultAsync(b => b.Id == request.BillId, cancellationToken)
            ?? throw new NotFoundException("Bill not found.");

        await BillGuards.EnsureDepartmentAccessAsync(db, currentUser, bill.ServiceJobId, cancellationToken); // Rule RB3/RB4
        BillGuards.EnsureEditable(bill); // Rule B3

        var line = await db.BillLineItems
                .FirstOrDefaultAsync(l => l.Id == request.LineId && l.BillId == request.BillId, cancellationToken)
            ?? throw new NotFoundException("Line item not found.");

        line.Type = request.Type;
        line.Description = request.Description.Trim();
        line.Quantity = request.Quantity;
        line.UnitPrice = request.UnitPrice;
        await db.SaveChangesAsync(cancellationToken);
    }
}
