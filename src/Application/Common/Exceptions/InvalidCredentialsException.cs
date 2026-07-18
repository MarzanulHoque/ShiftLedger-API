namespace ShiftLedger.Application.Common.Exceptions;

// Wrong credentials or an unusable/expired refresh token. Mapped to 401.
public class InvalidCredentialsException(string message = "Invalid credentials.") : Exception(message);
