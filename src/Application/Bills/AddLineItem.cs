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

public class AddLineItemCommandHandler(IAppDbContext db, ICurrentUser currentUser) : IRequestHandler<AddLineItemCommand, Guid>
{
    public async Task<Guid> Handle(AddLineItemCommand request, CancellationToken cancellationToken)
    {
        var bill = await db.Bills.AsNoTracking().FirstOrDefaultAsync(b => b.Id == request.BillId, cancellationToken)
            ?? throw new NotFoundException("Bill not found.");

        await BillGuards.EnsureDepartmentAccessAsync(db, currentUser, bill.ServiceJobId, cancellationToken);
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

    // Rule RB3/RB4: Bill/BillLineItem carry no department of their own (this codebase never adds
    // navigation properties — see P8 notes), so department scoping is derived from the bill's
    // parent job's own DepartmentId, compared explicitly against the caller (SuperAdmin bypasses,
    // RB0) — NOT via a global EF filter on ServiceJobs (see AppDbContext.OnModelCreating for why
    // that pattern is unsafe). A job outside the caller's department (or missing) reports as
    // Bill-not-found like the rest of this file, so a DepartmentAdmin can't distinguish "wrong
    // department" from "doesn't exist".
    public static async Task EnsureDepartmentAccessAsync(IAppDbContext db, ICurrentUser currentUser, Guid serviceJobId, CancellationToken ct)
    {
        if (currentUser.IsSuperAdmin)
        {
            return;
        }

        var departmentId = await db.ServiceJobs.AsNoTracking()
            .Where(j => j.Id == serviceJobId)
            .Select(j => (Guid?)j.DepartmentId)
            .FirstOrDefaultAsync(ct);
        if (departmentId is null || departmentId != currentUser.DepartmentId)
        {
            throw new NotFoundException("Bill not found.");
        }
    }
}
