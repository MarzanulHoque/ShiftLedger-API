namespace ShiftLedger.Application.Common.Exceptions;

// Thrown when a business rule is violated (e.g. SoD, immutability, locked edit). Mapped to 422.
public class BusinessRuleException(string message) : Exception(message);
