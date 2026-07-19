using ShiftLedger.Domain.Common;
using ShiftLedger.Domain.Enums;

namespace ShiftLedger.Domain.Entities;

// One labor/part line on a bill. LineTotal = Quantity × UnitPrice is computed, never stored
// (Rule B2). Quantity > 0 and UnitPrice >= 0 are enforced by validation (Rule B5).
public class BillLineItem : BaseEntity
{
    public Guid BillId { get; set; }
    public LineItemType Type { get; set; }
    public string Description { get; set; } = default!;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}
