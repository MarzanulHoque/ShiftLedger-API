namespace ShiftLedger.Application.Common.Exceptions;

// A requested entity does not exist. Mapped to 404.
public class NotFoundException(string message = "Not found.") : Exception(message);
