using DevicesApi.Domain;

namespace DevicesApi.Application;

public sealed class DeviceService(
    IDeviceRepository repository,
    TimeProvider timeProvider,
    ILogger<DeviceService> logger) : IDeviceService
{
    public const int MaxPageSize = 100;

    public async Task<DeviceResponse> CreateAsync(CreateDeviceRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var device = Device.Create(
            request.Name,
            request.Brand,
            request.State ?? DeviceState.Available,
            timeProvider.GetUtcNow());

        await repository.AddAsync(device, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Created device {DeviceId} ({Brand} {Name})", device.Id, device.Brand, device.Name);
        return device.ToResponse();
    }

    public async Task<DeviceResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var device = await GetOrThrowAsync(id, cancellationToken);
        return device.ToResponse();
    }

    public async Task<PagedResponse<DeviceResponse>> ListAsync(DeviceQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.Page < 1)
        {
            throw new DomainValidationException("page", "The page must be 1 or greater.");
        }

        if (query.PageSize is < 1 or > MaxPageSize)
        {
            throw new DomainValidationException("pageSize", $"The pageSize must be between 1 and {MaxPageSize}.");
        }

        var skip = (long)(query.Page - 1) * query.PageSize;
        if (skip > int.MaxValue)
        {
            throw new DomainValidationException("page", "The page is out of range.");
        }

        var brand = string.IsNullOrWhiteSpace(query.Brand) ? null : query.Brand.Trim();

        var result = await repository.SearchAsync(brand, query.State, (int)skip, query.PageSize, cancellationToken);

        return new PagedResponse<DeviceResponse>(
            result.Items.Select(d => d.ToResponse()).ToList(),
            query.Page,
            query.PageSize,
            result.TotalCount);
    }

    public async Task<DeviceResponse> ReplaceAsync(
        Guid id, ReplaceDeviceRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Name is null)
        {
            throw new DomainValidationException("name", "The name is required.");
        }

        if (request.Brand is null)
        {
            throw new DomainValidationException("brand", "The brand is required.");
        }

        if (request.State is null)
        {
            throw new DomainValidationException("state", "The state is required.");
        }

        return await UpdateAsync(id, request.Name, request.Brand, request.State, cancellationToken);
    }

    public async Task<DeviceResponse> PatchAsync(
        Guid id, PatchDeviceRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Name is null && request.Brand is null && request.State is null)
        {
            throw new DomainValidationException("body", "At least one of name, brand or state must be provided.");
        }

        return await UpdateAsync(id, request.Name, request.Brand, request.State, cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var device = await GetOrThrowAsync(id, cancellationToken);

        device.EnsureCanBeDeleted();

        repository.Remove(device);
        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Deleted device {DeviceId}", id);
    }

    private async Task<DeviceResponse> UpdateAsync(
        Guid id, string? name, string? brand, DeviceState? state, CancellationToken cancellationToken)
    {
        var device = await GetOrThrowAsync(id, cancellationToken);

        device.Update(name, brand, state);

        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Updated device {DeviceId}", id);
        return device.ToResponse();
    }

    private async Task<Device> GetOrThrowAsync(Guid id, CancellationToken cancellationToken) =>
        await repository.GetByIdAsync(id, cancellationToken) ?? throw new DeviceNotFoundException(id);
}
