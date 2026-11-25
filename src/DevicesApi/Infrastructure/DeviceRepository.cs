using DevicesApi.Application;
using DevicesApi.Domain;
using Microsoft.EntityFrameworkCore;

namespace DevicesApi.Infrastructure;

public sealed class DeviceRepository(DevicesDbContext dbContext) : IDeviceRepository
{
    public Task<Device?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Devices.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

    public async Task<PagedResult<Device>> SearchAsync(
        string? brand, DeviceState? state, int skip, int take, CancellationToken cancellationToken)
    {
        IQueryable<Device> query = dbContext.Devices.AsNoTracking();

        if (brand is not null)
        {
            var normalizedBrand = brand.ToLowerInvariant();
            query = query.Where(d => d.Brand.ToLower() == normalizedBrand);
        }

        if (state is not null)
        {
            var requiredState = state.Value;
            query = query.Where(d => d.State == requiredState);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(d => d.CreatedAt)
            .ThenBy(d => d.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        return new PagedResult<Device>(items, totalCount);
    }

    public async Task AddAsync(Device device, CancellationToken cancellationToken) =>
        await dbContext.Devices.AddAsync(device, cancellationToken);

    public void Remove(Device device) => dbContext.Devices.Remove(device);

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
