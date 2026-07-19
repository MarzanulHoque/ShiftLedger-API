using FluentAssertions;
using ShiftLedger.Application.Bills;
using ShiftLedger.Domain.Enums;
using Xunit;

namespace ShiftLedger.Application.UnitTests.Bills;

public class LineItemValidationTests
{
    private static readonly AddLineItemCommandValidator Validator = new();

    private static AddLineItemCommand Command(decimal qty, decimal price) =>
        new(Guid.NewGuid(), LineItemType.Part, "Brake pad set", qty, price);

    // Rule B5: Quantity must be > 0 and UnitPrice >= 0; violations fail validation (400).
    [Theory]
    [InlineData(0, 100)]    // zero quantity
    [InlineData(-1, 100)]   // negative quantity
    [InlineData(1, -0.01)]  // negative price
    public void Validate_OutOfBounds_Fails_B5(decimal qty, decimal price)
    {
        Validator.Validate(Command(qty, price)).IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(1, 0)]      // free line is allowed
    [InlineData(0.5, 40)]   // fractional labor hours
    public void Validate_WithinBounds_Passes_B5(decimal qty, decimal price)
    {
        Validator.Validate(Command(qty, price)).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyDescription_Fails_B5()
    {
        var command = new AddLineItemCommand(Guid.NewGuid(), LineItemType.Labor, "  ", 1m, 10m);
        Validator.Validate(command).IsValid.Should().BeFalse();
    }
}
