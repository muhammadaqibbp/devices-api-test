namespace DevicesApi.Application;

public interface IDeviceService
{
    Task<DeviceResponse> CreateAsync(CreateDeviceRequest request, CancellationToken cancellationToken);

    Task<DeviceResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<PagedResponse<DeviceResponse>> ListAsync(DeviceQuery query, CancellationToken cancellationToken);

    Task<DeviceResponse> ReplaceAsync(Guid id, ReplaceDeviceRequest request, CancellationToken cancellationToken);

    Task<DeviceResponse> PatchAsync(Guid id, PatchDeviceRequest request, CancellationToken cancellationToken);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}
