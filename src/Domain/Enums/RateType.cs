namespace ShiftLedger.Domain.Enums;

// How an employee's pay is expressed (Rule P2). Persisted as string (VARCHAR).
public enum RateType
{
    Hourly,
    Monthly,
    Fixed,
}
