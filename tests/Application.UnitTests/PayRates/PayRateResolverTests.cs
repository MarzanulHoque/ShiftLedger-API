using FluentAssertions;
using ShiftLedger.Application.PayRates;
using ShiftLedger.Domain.Entities;
using Xunit;

namespace ShiftLedger.Application.UnitTests.PayRates;

public class PayRateResolverTests
{
    // A profile paid 20 through Jun 2026, then 25 from Jul 1 onward (open-ended).
    private static readonly PayRate[] History =
    [
        new() { Amount = 20m, EffectiveFrom = new DateOnly(2026, 1, 1), EffectiveTo = new DateOnly(2026, 6, 30) },
        new() { Amount = 25m, EffectiveFrom = new DateOnly(2026, 7, 1), EffectiveTo = null },
    ];

    // Rule P3: the rate for a date is the one whose effective window contains it.
    [Theory]
    [InlineData("2026-03-15", 20)] // inside the closed window -> old rate
    [InlineData("2026-06-30", 20)] // last day of the closed window (inclusive)
    [InlineData("2026-07-01", 25)] // first day of the current window
    [InlineData("2026-12-31", 25)] // open-ended current rate applies indefinitely
    public void Resolve_ReturnsRateEffectiveOnDate(string date, decimal expected)
    {
        PayRateResolver.Resolve(History, DateOnly.Parse(date))!.Amount.Should().Be(expected);
    }

    // Rule P3: a date before any rate took effect resolves to nothing.
    [Fact]
    public void Resolve_BeforeAnyRate_ReturnsNull()
    {
        PayRateResolver.Resolve(History, new DateOnly(2025, 12, 31)).Should().BeNull();
    }
}
