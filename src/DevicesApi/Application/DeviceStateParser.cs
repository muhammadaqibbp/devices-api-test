using DevicesApi.Domain;

namespace DevicesApi.Application;

public static class DeviceStateParser
{
    public const string AllowedValues = "available, in-use, inactive";

    /// <summary>
    /// Parses a state supplied as text (e.g. a query string value). Case-insensitive; accepts
    /// "in-use", "in_use", "in use" and "inuse". Returns null for null/blank input.
    /// </summary>
    public static DeviceState? Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = new string(value.Where(c => c is not ('-' or '_' or ' ')).ToArray());

        if (normalized.Length > 0
            && normalized.All(char.IsLetter)
            && Enum.TryParse<DeviceState>(normalized, ignoreCase: true, out var state)
            && Enum.IsDefined(state))
        {
            return state;
        }

        throw new DomainValidationException(
            "state", $"'{value}' is not a valid state. Allowed values: {AllowedValues}.");
    }
}
