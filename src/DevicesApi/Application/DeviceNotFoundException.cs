namespace DevicesApi.Application;

public sealed class DeviceNotFoundException(Guid id) : Exception($"Device '{id}' was not found.")
{
    public Guid DeviceId { get; } = id;
}
