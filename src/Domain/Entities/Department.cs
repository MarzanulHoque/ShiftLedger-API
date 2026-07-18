using ShiftLedger.Domain.Common;

namespace ShiftLedger.Domain.Entities;

public class Department : BaseEntity
{
    public string Name { get; set; } = default!;
}
