using ShiftLedger.Domain.Enums;

namespace ShiftLedger.Application.Jobs;

// Rule J1: the service-job lifecycle is Received -> InProgress -> Completed -> Delivered.
// A status may advance exactly one step forward (anyone permitted on the job); an Admin may also
// step it back one to correct a mistake. Any other move (skips, jumps, no-op) is rejected.
public static class JobStatusFlow
{
    private static readonly JobStatus[] Order =
        [JobStatus.Received, JobStatus.InProgress, JobStatus.Completed, JobStatus.Delivered];

    public static bool CanTransition(JobStatus from, JobStatus to, bool isAdmin)
    {
        var delta = Array.IndexOf(Order, to) - Array.IndexOf(Order, from);
        return delta == 1 || (delta == -1 && isAdmin);
    }
}
