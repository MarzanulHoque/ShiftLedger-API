namespace ShiftLedger.Application.Common.Exceptions;

// The caller is authenticated but not allowed to act on this resource (e.g. an employee touching
// another mechanic's job — Rule R2/R3). Mapped to 403.
public class ForbiddenException(string message = "You do not have access to this resource.") : Exception(message);
