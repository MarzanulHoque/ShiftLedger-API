using FluentAssertions;
using ShiftLedger.Application.Bills;
using ShiftLedger.Domain.Entities;
using Xunit;

namespace ShiftLedger.Application.UnitTests.Bills;

public class BillMathTests
{
    // Rule B2: line total = quantity × unit price, rounded to the 2-decimal currency scale.
    [Theory]
    [InlineData(1, 500, 500)]        // single labor charge
    [InlineData(2, 300, 600)]        // 2 parts at 300
    [InlineData(1.5, 40, 60)]        // fractional quantity (1.5h labor at 40)
    [InlineData(3, 33.335, 100.01)]  // rounds half away from zero at the currency scale
    [InlineData(1, 0, 0)]            // free line (UnitPrice = 0 is allowed by B5)
    public void LineTotal_IsQuantityTimesPrice_Rounded_B2(decimal qty, decimal price, decimal expected)
    {
        BillMath.LineTotal(qty, price).Should().Be(expected);
    }

    // Rule B2: the bill total is the sum of its line totals — computed, never stored.
    [Fact]
    public void Total_SumsLineTotals_B2()
    {
        BillLineItem[] lines =
        [
            new() { Quantity = 1m, UnitPrice = 500m },   // labor: brake adjustment
            new() { Quantity = 2m, UnitPrice = 300m },   // parts: brake pad set ×2
        ];

        BillMath.Total(lines).Should().Be(1100m);
    }

    [Fact]
    public void Total_EmptyBill_IsZero_B2()
    {
        BillMath.Total([]).Should().Be(0m);
    }
}
