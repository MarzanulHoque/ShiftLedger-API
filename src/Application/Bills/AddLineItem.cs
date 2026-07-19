using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ShiftLedger.Application.Common.Exceptions;
using ShiftLedger.Application.Common.Interfaces;
using ShiftLedger.Domain.Entities;
using ShiftLedger.Domain.Enums;

namespace ShiftLedger.Application.Bills;

public record AddLineItemCommand(Guid BillId, LineItemType Type, string Description, decimal Quantity, decimal UnitPrice)
    : IRequest<Guid>;

// Rule B5: Quantity > 0, UnitPrice >= 0.
public class AddLineItemCommandValidator : AbstractValidator<AddLineItemCommand>
{
    public AddLineItemCommandValidator()
    {
        RuleFor(x => x.Description).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Quantity).GreaterThan(0);
        RuleFor(x => x.UnitPrice).GreaterThanOrEqualTo(0);
    }
}

public class AddLineItemCommandHandler(IAppDbContext db) : IRequestHandler<AddLineItemCommand, Guid>
{
    public async Task<Guid> Handle(AddLineItemCommand request, CancellationToken cancellationToken)
    {
        var bill = await db.Bills.AsNoTracking().FirstOrDefaultAsync(b => b.Id == request.BillId, cancellationToken)
            ?? throw new NotFoundException("Bill not found.");

        BillGuards.EnsureEditable(bill);

        var line = new BillLineItem
        {
            BillId = bill.Id,
            Type = request.Type,
            Description = request.Description.Trim(),
            Quantity = request.Quantity,
            UnitPrice = request.UnitPrice,
        };
        db.BillLineItems.Add(line);
        await db.SaveChangesAsync(cancellationToken);
        return line.Id;
    }
}

// Rule B3: a paid bill is edit-locked — no line may be added, changed, or removed.
internal static class BillGuards
{
    public static void EnsureEditable(Bill bill)
    {
        if (bill.IsPaid)
        {
            throw new BusinessRuleException("This bill is paid and locked. Reopen it (mark unpaid) to correct it.");
        }
    }
}
