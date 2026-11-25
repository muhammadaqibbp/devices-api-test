using DevicesApi.Domain;

namespace DevicesApi.Application;

public interface IDeviceRepository
{
    /// <summary>Returns a tracked device so changes can be persisted with <see cref="SaveChangesAsync"/>.</summary>
    Task<Device?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<PagedResult<Device>> SearchAsync(
        string? brand, DeviceState? state, int skip, int take, CancellationToken cancellationToken);

    Task AddAsync(Device device, CancellationToken cancellationToken);

    void Remove(Device device);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
