namespace VHSmart_Api.Shared.Exceptions;

// The credentials or the session were rejected (CodingRules 9): login answers 401 with the
// message, never with why the check failed (no user enumeration).
public sealed class UnauthorizedException(string message) : Exception(message) { }
