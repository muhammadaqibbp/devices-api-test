using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;

namespace DevicesApi.Tests.Integration;

/// <summary>
/// Boots the real application pipeline against a throw-away SQLite file database,
/// so HTTP behaviour, serialization, EF mappings and queries are exercised end to end.
/// </summary>
public sealed class DevicesApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databasePath =
        Path.Combine(Path.GetTempPath(), $"devices-api-tests-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Persistence:Provider", "Sqlite");
        builder.UseSetting("ConnectionStrings:Devices", $"Data Source={_databasePath}");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
        {
            return;
        }

        SqliteConnection.ClearAllPools();

        try
        {
            File.Delete(_databasePath);
        }
        catch (IOException)
        {
            // Best-effort cleanup of the temporary database file.
        }
    }
}
