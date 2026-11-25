using System.ComponentModel.DataAnnotations;
using DevicesApi.Domain;

namespace DevicesApi.Application;

/// <summary>Payload for creating a device.</summary>
public sealed class CreateDeviceRequest
{
    [Required, StringLength(Device.NameMaxLength)]
    public string? Name { get; init; }

    [Required, StringLength(Device.BrandMaxLength)]
    public string? Brand { get; init; }

    /// <summary>Optional. Defaults to <c>available</c>.</summary>
    public DeviceState? State { get; init; }
}

/// <summary>Payload for fully replacing the mutable fields of a device (PUT). All fields are required.</summary>
public sealed class ReplaceDeviceRequest
{
    [Required, StringLength(Device.NameMaxLength)]
    public string? Name { get; init; }

    [Required, StringLength(Device.BrandMaxLength)]
    public string? Brand { get; init; }

    [Required]
    public DeviceState? State { get; init; }
}

/// <summary>Payload for partially updating a device (PATCH). Omitted or null fields are left unchanged.</summary>
public sealed class PatchDeviceRequest
{
    [StringLength(Device.NameMaxLength)]
    public string? Name { get; init; }

    [StringLength(Device.BrandMaxLength)]
    public string? Brand { get; init; }

    public DeviceState? State { get; init; }
}

public sealed record DeviceResponse(
    Guid Id,
    string Name,
    string Brand,
    DeviceState State,
    DateTimeOffset CreationTime);

public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount);

public sealed record DeviceQuery(string? Brand, DeviceState? State, int Page, int PageSize);
