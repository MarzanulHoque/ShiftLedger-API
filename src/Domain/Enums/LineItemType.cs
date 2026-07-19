namespace ShiftLedger.Domain.Enums;

// A bill line is either labor or a part (Rule B5). Parts are free text in v1 — no inventory
// link; v2 adds a PartId to connect stock. Persisted as string (VARCHAR).
public enum LineItemType
{
    Labor,
    Part,
}
