namespace VHSmart_Api.Shared.Exceptions;

// Authenticated but not allowed (CodingRules 9).
public sealed class ForbiddenException(string message) : Exception(message) { }
