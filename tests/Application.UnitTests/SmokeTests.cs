using FluentAssertions;
using Xunit;

namespace ShiftLedger.Application.UnitTests;

/// <summary>
/// Placeholder proving the unit-test project builds and runs (phase P0).
/// Real rule-based tests (named with their Business Rule ID) are added per phase —
/// see plans/Testing_Plan.md.
/// </summary>
public class SmokeTests
{
    [Fact]
    public void TestProject_IsWiredUp()
    {
        var sum = 2 + 2;
        sum.Should().Be(4);
    }
}
