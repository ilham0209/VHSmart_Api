namespace VHSmart_Api.Shared.Exceptions;

// Duplicate / already exists. Message is the exact wording from the spec (CodingRules 9).
public sealed class ConflictException(string message) : Exception(message) { }
