using DevicesApi.Application;
using Microsoft.EntityFrameworkCore;

namespace DevicesApi.Infrastructure;

public static class PersistenceExtensions
{
    public const string ConnectionStringName = "Devices";
    public const string ProviderSettingName = "Persistence:Provider";

    /// <summary>
    /// Registers the DbContext and repository. PostgreSQL is the default and the supported production
    /// provider. "Sqlite" (file based) exists for the integration tests and quick local runs.
    /// </summary>
    public static IServiceCollection AddPersistence(this IServiceCollection services)
    {
        services.AddDbContext<DevicesDbContext>((serviceProvider, options) =>
        {
            var configuration = serviceProvider.GetRequiredService<IConfiguration>();

            var connectionString = configuration.GetConnectionString(ConnectionStringName)
                ?? throw new InvalidOperationException(
                    $"Connection string '{ConnectionStringName}' is not configured.");

            var provider = configuration[ProviderSettingName] ?? "PostgreSQL";

            switch (provider.ToLowerInvariant())
            {
                case "postgresql" or "postgres" or "npgsql":
                    options.UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure());
                    break;
                case "sqlite":
                    options.UseSqlite(connectionString);
                    break;
                default:
                    throw new InvalidOperationException(
                        $"Unsupported persistence provider '{provider}'. Use 'PostgreSQL' or 'Sqlite'.");
            }
        });

        services.AddScoped<IDeviceRepository, DeviceRepository>();
        return services;
    }

    /// <summary>Creates the schema if it does not exist yet. See README for the migrations follow-up.</summary>
    public static async Task InitializeDatabaseAsync(this WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DevicesDbContext>();
        await dbContext.Database.EnsureCreatedAsync();
    }
}
