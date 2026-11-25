namespace DevicesApi.Domain;

public abstract class DomainException(string message) : Exception(message);

/// <summary>An input value violates a structural rule (missing, blank, too long, unknown).</summary>
public sealed class DomainValidationException(string field, string message) : DomainException(message)
{
    public string Field { get; } = field;
}

/// <summary>A well-formed request is rejected because of the current state of the device.</summary>
public sealed class BusinessRuleViolationException(string message) : DomainException(message);
