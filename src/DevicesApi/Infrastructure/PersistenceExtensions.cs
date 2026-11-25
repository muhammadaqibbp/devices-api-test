using DevicesApi.Application;
using Microsoft.EntityFrameworkCore;

namespace DevicesApi.Infrastructure;

public static class PersistenceExtensions
{
    public const string ConnectionStringName = "Devices";
    public const string ProviderSettingName = "Persistence:Provider";

    /// <summary>
    /// Registers the DbContext and repository. SQL Server is the default and the supported production
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

            var provider = configuration[ProviderSettingName] ?? "SqlServer";

            switch (provider.ToLowerInvariant())
            {
                case "sqlserver" or "mssql":
                    options.UseSqlServer(connectionString, sqlServer => sqlServer.EnableRetryOnFailure());
                    break;
                case "sqlite":
                    options.UseSqlite(connectionString);
                    break;
                default:
                    throw new InvalidOperationException(
                        $"Unsupported persistence provider '{provider}'. Use 'SqlServer' or 'Sqlite'.");
            }
        });

        services.AddScoped<IDeviceRepository, DeviceRepository>();
        return services;
    }

    /// <summary>
    /// Prepares the database at startup. On SQL Server the database is created if it does not exist and
    /// all pending migrations are applied. SQLite (tests / quick local runs) has no migrations and its
    /// schema is created directly from the model.
    /// </summary>
    public static async Task InitializeDatabaseAsync(this WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DevicesDbContext>();

        if (dbContext.Database.IsSqlite())
        {
            await dbContext.Database.EnsureCreatedAsync();
            return;
        }

        if (!dbContext.Database.GetMigrations().Any())
        {
            throw new InvalidOperationException(
                "No EF Core migrations were found. Run: dotnet ef migrations add InitialCreate " +
                "--project src/DevicesApi --output-dir Infrastructure/Migrations");
        }

        await dbContext.Database.MigrateAsync();
    }
}
