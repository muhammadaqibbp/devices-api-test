using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using DevicesApi.Api;
using DevicesApi.Application;
using DevicesApi.Infrastructure;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        // Enum values are exposed as "available", "in-use" and "inactive".
        options.JsonSerializerOptions.Converters.Add(
            new JsonStringEnumConverter(JsonNamingPolicy.KebabCaseLower, allowIntegerValues: false));

        // Reject unknown properties, e.g. a client trying to send "creationTime" on an update.
        options.JsonSerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
    });

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddPersistence();
builder.Services.AddScoped<IDeviceService, DeviceService>();

builder.Services.AddHealthChecks().AddDbContextCheck<DevicesDbContext>("database");

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Devices API",
        Version = "v1",
        Description = "REST API for persisting and managing device resources."
    });

    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFile));
});

var app = builder.Build();

await app.InitializeDatabaseAsync();

app.UseExceptionHandler();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Devices API v1");
    options.RoutePrefix = "swagger";
});

app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();
app.MapHealthChecks("/health");
app.MapControllers();

app.Run();

// Exposed so integration tests can bootstrap the application with WebApplicationFactory.
public partial class Program { }
