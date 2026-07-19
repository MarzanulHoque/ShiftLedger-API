using ShiftLedger.Domain.Entities;

namespace ShiftLedger.Application.Bills;

// Rule B2: a bill's money is always computed from its line items, never stored. Each line total
// (and the bill total) rounds to the 2-decimal currency scale so DB and display always agree.
public static class BillMath
{
    public static decimal LineTotal(decimal quantity, decimal unitPrice) =>
        Math.Round(quantity * unitPrice, 2, MidpointRounding.AwayFromZero);

    public static decimal Total(IEnumerable<BillLineItem> lines) =>
        lines.Sum(l => LineTotal(l.Quantity, l.UnitPrice));
}
