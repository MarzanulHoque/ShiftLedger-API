namespace ShiftLedger.Application.Common.Messaging;

// Marker for commands that must run inside a single DB transaction (Rule C4).
// TransactionBehavior wraps only requests implementing this; queries are left untouched.
public interface ITransactionalRequest;
