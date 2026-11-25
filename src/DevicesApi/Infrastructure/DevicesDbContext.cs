using DevicesApi.Domain;
using Microsoft.EntityFrameworkCore;

namespace DevicesApi.Infrastructure;

public sealed class DevicesDbContext(DbContextOptions<DevicesDbContext> options) : DbContext(options)
{
    public DbSet<Device> Devices => Set<Device>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DevicesDbContext).Assembly);
}
