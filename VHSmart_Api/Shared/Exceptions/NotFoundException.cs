namespace VHSmart_Api.Shared.Exceptions;

// Record not found, or exists in another tenant so it must look missing (CodingRules 9).
public sealed class NotFoundException(string message) : Exception(message) { }
