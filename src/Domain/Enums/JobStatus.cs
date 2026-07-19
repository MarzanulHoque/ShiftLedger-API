namespace ShiftLedger.Domain.Enums;

// Service-job lifecycle (Rule J1): forward Received -> InProgress -> Completed -> Delivered.
// Persisted as string (VARCHAR). Transition legality lives in Application/Jobs/JobStatusFlow.
public enum JobStatus
{
    Received,
    InProgress,
    Completed,
    Delivered,
}
