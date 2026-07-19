using FluentAssertions;
using ShiftLedger.Application.Jobs;
using ShiftLedger.Domain.Enums;
using Xunit;

namespace ShiftLedger.Application.UnitTests.Jobs;

public class JobStatusFlowTests
{
    // Rule J1: a job advances exactly one step forward through Received->InProgress->Completed->Delivered.
    [Theory]
    [InlineData(JobStatus.Received, JobStatus.InProgress)]
    [InlineData(JobStatus.InProgress, JobStatus.Completed)]
    [InlineData(JobStatus.Completed, JobStatus.Delivered)]
    public void CanTransition_OneStepForward_Allowed(JobStatus from, JobStatus to)
    {
        JobStatusFlow.CanTransition(from, to, isAdmin: false).Should().BeTrue();
    }

    // Rule J1: skipping a step (or jumping straight to Delivered) is rejected for anyone.
    [Theory]
    [InlineData(JobStatus.Received, JobStatus.Completed)]
    [InlineData(JobStatus.Received, JobStatus.Delivered)]
    [InlineData(JobStatus.InProgress, JobStatus.Delivered)]
    public void CanTransition_Skip_Rejected(JobStatus from, JobStatus to)
    {
        JobStatusFlow.CanTransition(from, to, isAdmin: true).Should().BeFalse();
    }

    // Rule J1: only an Admin may step a job back one status to correct a mistake.
    [Fact]
    public void CanTransition_OneStepBack_AdminOnly()
    {
        JobStatusFlow.CanTransition(JobStatus.Completed, JobStatus.InProgress, isAdmin: true).Should().BeTrue();
        JobStatusFlow.CanTransition(JobStatus.Completed, JobStatus.InProgress, isAdmin: false).Should().BeFalse();
    }

    // A no-op (same status) is not a valid transition.
    [Fact]
    public void CanTransition_SameStatus_Rejected()
    {
        JobStatusFlow.CanTransition(JobStatus.Received, JobStatus.Received, isAdmin: true).Should().BeFalse();
    }
}
