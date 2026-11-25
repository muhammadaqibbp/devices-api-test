using DevicesApi.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace DevicesApi.Infrastructure;

public sealed class DeviceConfiguration : IEntityTypeConfiguration<Device>
{
    // Stored as a UTC DateTime so ordering and comparison work on every provider.
    private static readonly ValueConverter<DateTimeOffset, DateTime> UtcConverter = new(
        value => value.UtcDateTime,
        value => new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc)));

    public void Configure(EntityTypeBuilder<Device> builder)
    {
        builder.ToTable("devices");

        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedNever();

        builder.Property(d => d.Name).IsRequired().HasMaxLength(Device.NameMaxLength);
        builder.Property(d => d.Brand).IsRequired().HasMaxLength(Device.BrandMaxLength);

        builder.Property(d => d.State).IsRequired().HasConversion<string>().HasMaxLength(20);

        builder.Property(d => d.CreatedAt).IsRequired().HasConversion(UtcConverter);

        builder.Property(d => d.Version).IsConcurrencyToken();

        builder.HasIndex(d => d.Brand);
        builder.HasIndex(d => d.State);
        builder.HasIndex(d => d.CreatedAt);
    }
}
