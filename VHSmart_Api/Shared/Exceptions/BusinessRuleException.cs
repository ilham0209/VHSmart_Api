namespace VHSmart_Api.Shared.Exceptions;

// Business rule broken, for example "at least one Recommendation" (CodingRules 9).
public sealed class BusinessRuleException(string message) : Exception(message) { }
