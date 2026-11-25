using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using DevicesApi.Application;
using DevicesApi.Domain;
using Xunit;

namespace DevicesApi.Tests.Integration;

public class DevicesApiTests(DevicesApiFactory factory) : IClassFixture<DevicesApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.KebabCaseLower) }
    };

    private readonly HttpClient _client = factory.CreateClient();

    private static string UniqueBrand() => $"Brand-{Guid.NewGuid():N}";

    private async Task<DeviceResponse> CreateAsync(string name, string brand, string state = "available")
    {
        var response = await _client.PostAsJsonAsync("/api/devices", new { name, brand, state });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<DeviceResponse>(Json))!;
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<T>(Json))!;

    private Task<HttpResponseMessage> PatchAsync(Guid id, object body) =>
        _client.PatchAsync($"/api/devices/{id}", JsonContent.Create(body));

    // ---- Create ----

    [Fact]
    public async Task Post_WithValidDevice_Returns201WithLocationAndBody()
    {
        var brand = UniqueBrand();

        var response = await _client.PostAsJsonAsync("/api/devices", new { name = "iPhone 15", brand, state = "in-use" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var device = await ReadAsync<DeviceResponse>(response);
        Assert.Equal("iPhone 15", device.Name);
        Assert.Equal(brand, device.Brand);
        Assert.Equal(DeviceState.InUse, device.State);
        Assert.True(device.CreationTime > DateTimeOffset.UtcNow.AddMinutes(-5));
        Assert.EndsWith($"/api/devices/{device.Id}", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Post_WithoutState_DefaultsToAvailable()
    {
        var response = await _client.PostAsJsonAsync("/api/devices", new { name = "Pixel", brand = UniqueBrand() });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(DeviceState.Available, (await ReadAsync<DeviceResponse>(response)).State);
    }

    [Theory]
    [InlineData("", "Apple")]
    [InlineData("   ", "Apple")]
    [InlineData("iPhone", "")]
    [InlineData("iPhone", "   ")]
    public async Task Post_WithBlankNameOrBrand_Returns400(string name, string brand)
    {
        var response = await _client.PostAsJsonAsync("/api/devices", new { name, brand });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Post_WithMissingBrand_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/devices", new { name = "iPhone" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Post_WithTooLongName_Returns400()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/devices", new { name = new string('a', Device.NameMaxLength + 1), brand = "Apple" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("\"broken\"")]
    [InlineData("2")]
    [InlineData("\"\"")]
    public async Task Post_WithInvalidState_Returns400(string stateJson)
    {
        var content = new StringContent(
            $$"""{"name":"iPhone","brand":"Apple","state":{{stateJson}}}""",
            System.Text.Encoding.UTF8,
            "application/json");

        var response = await _client.PostAsync("/api/devices", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Post_WithCreationTimeInBody_Returns400()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/devices", new { name = "iPhone", brand = "Apple", creationTime = "2000-01-01T00:00:00Z" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---- Get one ----

    [Fact]
    public async Task Get_ExistingDevice_Returns200()
    {
        var created = await CreateAsync("iPhone 15", UniqueBrand());

        var response = await _client.GetAsync($"/api/devices/{created.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var device = await ReadAsync<DeviceResponse>(response);
        Assert.Equal(created.Id, device.Id);
        Assert.Equal(created.Name, device.Name);
        Assert.Equal(created.CreationTime, device.CreationTime);
    }

    [Fact]
    public async Task Get_UnknownDevice_Returns404()
    {
        var response = await _client.GetAsync($"/api/devices/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Get_WithMalformedId_Returns404()
    {
        var response = await _client.GetAsync("/api/devices/not-a-guid");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---- Get many ----

    [Fact]
    public async Task List_WithoutFilters_ReturnsCreatedDevices()
    {
        var brand = UniqueBrand();
        var a = await CreateAsync("A", brand);
        var b = await CreateAsync("B", brand, "inactive");

        var response = await _client.GetAsync("/api/devices?pageSize=100");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await ReadAsync<PagedResponse<DeviceResponse>>(response);
        Assert.Contains(page.Items, d => d.Id == a.Id);
        Assert.Contains(page.Items, d => d.Id == b.Id);
    }

    [Fact]
    public async Task List_ByBrand_ReturnsOnlyThatBrandIgnoringCase()
    {
        var brand = UniqueBrand();
        var other = UniqueBrand();
        await CreateAsync("A", brand);
        await CreateAsync("B", brand, "in-use");
        await CreateAsync("C", other);

        var response = await _client.GetAsync($"/api/devices?brand={brand.ToUpperInvariant()}");

        var page = await ReadAsync<PagedResponse<DeviceResponse>>(response);
        Assert.Equal(2, page.TotalCount);
        Assert.All(page.Items, d => Assert.Equal(brand, d.Brand));
    }

    [Fact]
    public async Task List_ByState_ReturnsOnlyThatState()
    {
        var brand = UniqueBrand();
        await CreateAsync("A", brand, "available");
        var inUse = await CreateAsync("B", brand, "in-use");
        await CreateAsync("C", brand, "inactive");

        var response = await _client.GetAsync("/api/devices?state=in-use&pageSize=100");

        var page = await ReadAsync<PagedResponse<DeviceResponse>>(response);
        Assert.Contains(page.Items, d => d.Id == inUse.Id);
        Assert.All(page.Items, d => Assert.Equal(DeviceState.InUse, d.State));
    }

    [Fact]
    public async Task List_ByBrandAndState_AppliesBothFilters()
    {
        var brand = UniqueBrand();
        await CreateAsync("A", brand, "available");
        var match = await CreateAsync("B", brand, "inactive");

        var response = await _client.GetAsync($"/api/devices?brand={brand}&state=inactive");

        var page = await ReadAsync<PagedResponse<DeviceResponse>>(response);
        var only = Assert.Single(page.Items);
        Assert.Equal(match.Id, only.Id);
    }

    [Fact]
    public async Task List_WithUnknownState_Returns400()
    {
        var response = await _client.GetAsync("/api/devices?state=broken");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=101")]
    [InlineData("page=abc")]
    public async Task List_WithInvalidPaging_Returns400(string query)
    {
        var response = await _client.GetAsync($"/api/devices?{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task List_Paging_ReturnsRequestedSliceInCreationOrder()
    {
        var brand = UniqueBrand();
        var first = await CreateAsync("1", brand);
        var second = await CreateAsync("2", brand);
        var third = await CreateAsync("3", brand);

        var page1 = await ReadAsync<PagedResponse<DeviceResponse>>(
            await _client.GetAsync($"/api/devices?brand={brand}&page=1&pageSize=2"));
        var page2 = await ReadAsync<PagedResponse<DeviceResponse>>(
            await _client.GetAsync($"/api/devices?brand={brand}&page=2&pageSize=2"));

        Assert.Equal(3, page1.TotalCount);
        Assert.Equal(2, page1.TotalPages);
        Assert.Equal(new[] { first.Id, second.Id }, page1.Items.Select(d => d.Id).ToArray());
        Assert.Equal(new[] { third.Id }, page2.Items.Select(d => d.Id).ToArray());
    }

    // ---- Full update (PUT) ----

    [Fact]
    public async Task Put_OnAvailableDevice_UpdatesAllFieldsAndKeepsCreationTime()
    {
        var created = await CreateAsync("iPhone 15", UniqueBrand());
        var newBrand = UniqueBrand();

        var response = await _client.PutAsJsonAsync(
            $"/api/devices/{created.Id}", new { name = "Galaxy", brand = newBrand, state = "inactive" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await ReadAsync<DeviceResponse>(response);
        Assert.Equal("Galaxy", updated.Name);
        Assert.Equal(newBrand, updated.Brand);
        Assert.Equal(DeviceState.Inactive, updated.State);
        Assert.Equal(created.CreationTime, updated.CreationTime);

        var reloaded = await ReadAsync<DeviceResponse>(await _client.GetAsync($"/api/devices/{created.Id}"));
        Assert.Equal("Galaxy", reloaded.Name);
        Assert.Equal(created.CreationTime, reloaded.CreationTime);
    }

    [Fact]
    public async Task Put_OnInUseDevice_ChangingName_Returns409AndKeepsData()
    {
        var created = await CreateAsync("iPhone 15", UniqueBrand(), "in-use");

        var response = await _client.PutAsJsonAsync(
            $"/api/devices/{created.Id}", new { name = "Renamed", brand = created.Brand, state = "in-use" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var reloaded = await ReadAsync<DeviceResponse>(await _client.GetAsync($"/api/devices/{created.Id}"));
        Assert.Equal("iPhone 15", reloaded.Name);
    }

    [Fact]
    public async Task Put_OnInUseDevice_ChangingBrand_Returns409()
    {
        var created = await CreateAsync("iPhone 15", UniqueBrand(), "in-use");

        var response = await _client.PutAsJsonAsync(
            $"/api/devices/{created.Id}", new { name = created.Name, brand = UniqueBrand(), state = "in-use" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Put_OnInUseDevice_ChangingOnlyState_Returns200()
    {
        var created = await CreateAsync("iPhone 15", UniqueBrand(), "in-use");

        var response = await _client.PutAsJsonAsync(
            $"/api/devices/{created.Id}", new { name = created.Name, brand = created.Brand, state = "available" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(DeviceState.Available, (await ReadAsync<DeviceResponse>(response)).State);
    }

    [Fact]
    public async Task Put_WithMissingState_Returns400()
    {
        var created = await CreateAsync("iPhone 15", UniqueBrand());

        var response = await _client.PutAsJsonAsync(
            $"/api/devices/{created.Id}", new { name = "X", brand = "Y" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Put_WithCreationTimeInBody_Returns400AndKeepsCreationTime()
    {
        var created = await CreateAsync("iPhone 15", UniqueBrand());

        var response = await _client.PutAsJsonAsync(
            $"/api/devices/{created.Id}",
            new { name = "X", brand = "Y", state = "available", creationTime = "2000-01-01T00:00:00Z" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var reloaded = await ReadAsync<DeviceResponse>(await _client.GetAsync($"/api/devices/{created.Id}"));
        Assert.Equal(created.CreationTime, reloaded.CreationTime);
    }

    [Fact]
    public async Task Put_OnUnknownDevice_Returns404()
    {
        var response = await _client.PutAsJsonAsync(
            $"/api/devices/{Guid.NewGuid()}", new { name = "X", brand = "Y", state = "available" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---- Partial update (PATCH) ----

    [Fact]
    public async Task Patch_WithSingleField_UpdatesOnlyThatField()
    {
        var created = await CreateAsync("iPhone 15", UniqueBrand());

        var response = await PatchAsync(created.Id, new { state = "in-use" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await ReadAsync<DeviceResponse>(response);
        Assert.Equal(DeviceState.InUse, updated.State);
        Assert.Equal(created.Name, updated.Name);
        Assert.Equal(created.Brand, updated.Brand);
        Assert.Equal(created.CreationTime, updated.CreationTime);
    }

    [Fact]
    public async Task Patch_OnInUseDevice_ChangingName_Returns409()
    {
        var created = await CreateAsync("iPhone 15", UniqueBrand(), "in-use");

        var response = await PatchAsync(created.Id, new { name = "Renamed" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Patch_WithEmptyBody_Returns400()
    {
        var created = await CreateAsync("iPhone 15", UniqueBrand());

        var response = await PatchAsync(created.Id, new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Patch_WithCreationTime_Returns400()
    {
        var created = await CreateAsync("iPhone 15", UniqueBrand());

        var response = await PatchAsync(created.Id, new { creationTime = "2000-01-01T00:00:00Z" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Patch_OnUnknownDevice_Returns404()
    {
        var response = await PatchAsync(Guid.NewGuid(), new { name = "X" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---- Delete ----

    [Theory]
    [InlineData("available")]
    [InlineData("inactive")]
    public async Task Delete_WhenNotInUse_Returns204AndRemovesDevice(string state)
    {
        var created = await CreateAsync("iPhone 15", UniqueBrand(), state);

        var response = await _client.DeleteAsync($"/api/devices/{created.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/devices/{created.Id}")).StatusCode);
    }

    [Fact]
    public async Task Delete_WhenInUse_Returns409AndKeepsDevice()
    {
        var created = await CreateAsync("iPhone 15", UniqueBrand(), "in-use");

        var response = await _client.DeleteAsync($"/api/devices/{created.Id}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync($"/api/devices/{created.Id}")).StatusCode);
    }

    [Fact]
    public async Task Delete_AfterDeviceLeavesInUse_Succeeds()
    {
        var created = await CreateAsync("iPhone 15", UniqueBrand(), "in-use");
        await PatchAsync(created.Id, new { state = "available" });

        var response = await _client.DeleteAsync($"/api/devices/{created.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Delete_UnknownDevice_Returns404()
    {
        var response = await _client.DeleteAsync($"/api/devices/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---- Platform ----

    [Fact]
    public async Task Health_ReturnsHealthy()
    {
        var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task OpenApiDocument_IsServed()
    {
        var response = await _client.GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("/api/devices", await response.Content.ReadAsStringAsync());
    }
}
