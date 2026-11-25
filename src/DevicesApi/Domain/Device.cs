namespace DevicesApi.Domain;

public sealed class Device
{
    public const int NameMaxLength = 200;
    public const int BrandMaxLength = 100;

    // Required by EF Core.
    private Device()
    {
        Name = string.Empty;
        Brand = string.Empty;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public string Brand { get; private set; }
    public DeviceState State { get; private set; }

    /// <summary>Set once on creation. There is intentionally no way to change it.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Optimistic concurrency token, incremented on every effective change.</summary>
    public int Version { get; private set; }

    public static Device Create(string? name, string? brand, DeviceState state, DateTimeOffset createdAt)
    {
        return new Device
        {
            Id = Guid.CreateVersion7(createdAt),
            Name = RequireText(name, "name", NameMaxLength),
            Brand = RequireText(brand, "brand", BrandMaxLength),
            State = RequireDefined(state),
            CreatedAt = createdAt
        };
    }

    /// <summary>
    /// Applies a full or partial update. A null argument means "leave unchanged".
    /// Name and brand are locked while the device is in use; the state itself may always change.
    /// Sending the current value again is not considered a change.
    /// </summary>
    public void Update(string? name, string? brand, DeviceState? state)
    {
        var newName = name is null ? Name : RequireText(name, "name", NameMaxLength);
        var newBrand = brand is null ? Brand : RequireText(brand, "brand", BrandMaxLength);
        var newState = state is null ? State : RequireDefined(state.Value);

        var identityChanged = !string.Equals(newName, Name, StringComparison.Ordinal)
                              || !string.Equals(newBrand, Brand, StringComparison.Ordinal);

        if (State == DeviceState.InUse && identityChanged)
        {
            throw new BusinessRuleViolationException(
                "The name and brand of a device that is in use cannot be updated.");
        }

        if (!identityChanged && newState == State)
        {
            return;
        }

        Name = newName;
        Brand = newBrand;
        State = newState;
        Version++;
    }

    public void EnsureCanBeDeleted()
    {
        if (State == DeviceState.InUse)
        {
            throw new BusinessRuleViolationException("A device that is in use cannot be deleted.");
        }
    }

    private static string RequireText(string? value, string field, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainValidationException(field, $"The {field} is required and cannot be blank.");
        }

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            throw new DomainValidationException(field, $"The {field} cannot exceed {maxLength} characters.");
        }

        return trimmed;
    }

    private static DeviceState RequireDefined(DeviceState state)
    {
        if (!Enum.IsDefined(state))
        {
            throw new DomainValidationException("state", "The state is not valid.");
        }

        return state;
    }
}
