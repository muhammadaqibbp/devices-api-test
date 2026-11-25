using DevicesApi.Application;
using DevicesApi.Domain;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace DevicesApi.Tests.Application;

public class DeviceServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 8, 30, 0, TimeSpan.Zero);

    private readonly IDeviceRepository _repository = Substitute.For<IDeviceRepository>();
    private readonly DeviceService _sut;

    public DeviceServiceTests()
    {
        _sut = new DeviceService(_repository, new FixedTimeProvider(Now), NullLogger<DeviceService>.Instance);
    }

    private Device GivenStoredDevice(DeviceState state = DeviceState.Available)
    {
        var device = Device.Create("iPhone 15", "Apple", state, Now.AddDays(-1));
        _repository.GetByIdAsync(device.Id, Arg.Any<CancellationToken>()).Returns(device);
        return device;
    }

    private Guid GivenMissingDevice()
    {
        var id = Guid.NewGuid();
        _repository.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns((Device?)null);
        return id;
    }

    // ---- Create ----

    [Fact]
    public async Task CreateAsync_PersistsDeviceWithServerGeneratedCreationTime()
    {
        var request = new CreateDeviceRequest { Name = "Pixel 9", Brand = "Google", State = DeviceState.InUse };

        var response = await _sut.CreateAsync(request, CancellationToken.None);

        Assert.Equal("Pixel 9", response.Name);
        Assert.Equal("Google", response.Brand);
        Assert.Equal(DeviceState.InUse, response.State);
        Assert.Equal(Now, response.CreationTime);
        Assert.NotEqual(Guid.Empty, response.Id);
        await _repository.Received(1).AddAsync(
            Arg.Is<Device>(d => d.Id == response.Id), Arg.Any<CancellationToken>());
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_WithoutState_DefaultsToAvailable()
    {
        var request = new CreateDeviceRequest { Name = "Pixel 9", Brand = "Google" };

        var response = await _sut.CreateAsync(request, CancellationToken.None);

        Assert.Equal(DeviceState.Available, response.State);
    }

    [Fact]
    public async Task CreateAsync_WithBlankName_ThrowsAndDoesNotPersist()
    {
        var request = new CreateDeviceRequest { Name = " ", Brand = "Google" };

        await Assert.ThrowsAsync<DomainValidationException>(() => _sut.CreateAsync(request, CancellationToken.None));

        await _repository.DidNotReceive().AddAsync(Arg.Any<Device>(), Arg.Any<CancellationToken>());
        await _repository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_WithNullRequest_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _sut.CreateAsync(null!, CancellationToken.None));
    }

    // ---- Get ----

    [Fact]
    public async Task GetByIdAsync_WhenFound_ReturnsDevice()
    {
        var device = GivenStoredDevice();

        var response = await _sut.GetByIdAsync(device.Id, CancellationToken.None);

        Assert.Equal(device.Id, response.Id);
        Assert.Equal(device.Name, response.Name);
        Assert.Equal(device.CreatedAt, response.CreationTime);
    }

    [Fact]
    public async Task GetByIdAsync_WhenMissing_ThrowsNotFound()
    {
        var id = GivenMissingDevice();

        var ex = await Assert.ThrowsAsync<DeviceNotFoundException>(() => _sut.GetByIdAsync(id, CancellationToken.None));

        Assert.Equal(id, ex.DeviceId);
    }

    // ---- List ----

    [Fact]
    public async Task ListAsync_PassesFiltersAndPagingToRepository()
    {
        var device = Device.Create("iPhone", "Apple", DeviceState.Available, Now);
        _repository
            .SearchAsync("Apple", DeviceState.Available, 20, 10, Arg.Any<CancellationToken>())
            .Returns(new PagedResult<Device>([device], 31));

        var response = await _sut.ListAsync(
            new DeviceQuery("  Apple ", DeviceState.Available, Page: 3, PageSize: 10), CancellationToken.None);

        Assert.Single(response.Items);
        Assert.Equal(3, response.Page);
        Assert.Equal(10, response.PageSize);
        Assert.Equal(31, response.TotalCount);
        Assert.Equal(4, response.TotalPages);
    }

    [Fact]
    public async Task ListAsync_WithBlankBrand_DoesNotFilterByBrand()
    {
        _repository
            .SearchAsync(null, null, 0, 20, Arg.Any<CancellationToken>())
            .Returns(new PagedResult<Device>([], 0));

        var response = await _sut.ListAsync(new DeviceQuery("  ", null, 1, 20), CancellationToken.None);

        Assert.Empty(response.Items);
        await _repository.Received(1).SearchAsync(null, null, 0, 20, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(0, 10, "page")]
    [InlineData(-1, 10, "page")]
    [InlineData(1, 0, "pageSize")]
    [InlineData(1, DeviceService.MaxPageSize + 1, "pageSize")]
    [InlineData(int.MaxValue, DeviceService.MaxPageSize, "page")]
    public async Task ListAsync_WithInvalidPaging_Throws(int page, int pageSize, string expectedField)
    {
        var ex = await Assert.ThrowsAsync<DomainValidationException>(
            () => _sut.ListAsync(new DeviceQuery(null, null, page, pageSize), CancellationToken.None));

        Assert.Equal(expectedField, ex.Field);
    }

    // ---- Replace (PUT) ----

    [Fact]
    public async Task ReplaceAsync_WhenAvailable_UpdatesAllFieldsAndSaves()
    {
        var device = GivenStoredDevice(DeviceState.Available);
        var request = new ReplaceDeviceRequest { Name = "Galaxy S24", Brand = "Samsung", State = DeviceState.Inactive };

        var response = await _sut.ReplaceAsync(device.Id, request, CancellationToken.None);

        Assert.Equal("Galaxy S24", response.Name);
        Assert.Equal("Samsung", response.Brand);
        Assert.Equal(DeviceState.Inactive, response.State);
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReplaceAsync_DoesNotChangeCreationTime()
    {
        var device = GivenStoredDevice();
        var originalCreationTime = device.CreatedAt;
        var request = new ReplaceDeviceRequest { Name = "X", Brand = "Y", State = DeviceState.Inactive };

        var response = await _sut.ReplaceAsync(device.Id, request, CancellationToken.None);

        Assert.Equal(originalCreationTime, response.CreationTime);
    }

    [Fact]
    public async Task ReplaceAsync_WhenInUseAndNameChanges_ThrowsAndDoesNotSave()
    {
        var device = GivenStoredDevice(DeviceState.InUse);
        var request = new ReplaceDeviceRequest { Name = "Renamed", Brand = "Apple", State = DeviceState.InUse };

        await Assert.ThrowsAsync<BusinessRuleViolationException>(
            () => _sut.ReplaceAsync(device.Id, request, CancellationToken.None));

        await _repository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReplaceAsync_WhenInUseAndOnlyStateChanges_Succeeds()
    {
        var device = GivenStoredDevice(DeviceState.InUse);
        var request = new ReplaceDeviceRequest { Name = "iPhone 15", Brand = "Apple", State = DeviceState.Available };

        var response = await _sut.ReplaceAsync(device.Id, request, CancellationToken.None);

        Assert.Equal(DeviceState.Available, response.State);
    }

    [Fact]
    public async Task ReplaceAsync_WhenMissing_ThrowsNotFound()
    {
        var id = GivenMissingDevice();
        var request = new ReplaceDeviceRequest { Name = "X", Brand = "Y", State = DeviceState.Available };

        await Assert.ThrowsAsync<DeviceNotFoundException>(() => _sut.ReplaceAsync(id, request, CancellationToken.None));
    }

    [Theory]
    [InlineData(null, "Brand", DeviceState.Available, "name")]
    [InlineData("Name", null, DeviceState.Available, "brand")]
    [InlineData("Name", "Brand", null, "state")]
    public async Task ReplaceAsync_WithMissingField_Throws(
        string? name, string? brand, DeviceState? state, string expectedField)
    {
        var device = GivenStoredDevice();
        var request = new ReplaceDeviceRequest { Name = name, Brand = brand, State = state };

        var ex = await Assert.ThrowsAsync<DomainValidationException>(
            () => _sut.ReplaceAsync(device.Id, request, CancellationToken.None));

        Assert.Equal(expectedField, ex.Field);
    }

    // ---- Patch (PATCH) ----

    [Fact]
    public async Task PatchAsync_WithOnlyState_UpdatesOnlyState()
    {
        var device = GivenStoredDevice(DeviceState.Available);

        var response = await _sut.PatchAsync(
            device.Id, new PatchDeviceRequest { State = DeviceState.InUse }, CancellationToken.None);

        Assert.Equal(DeviceState.InUse, response.State);
        Assert.Equal("iPhone 15", response.Name);
        Assert.Equal("Apple", response.Brand);
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PatchAsync_WithOnlyName_UpdatesOnlyName()
    {
        var device = GivenStoredDevice(DeviceState.Inactive);

        var response = await _sut.PatchAsync(
            device.Id, new PatchDeviceRequest { Name = "iPhone 16" }, CancellationToken.None);

        Assert.Equal("iPhone 16", response.Name);
        Assert.Equal("Apple", response.Brand);
        Assert.Equal(DeviceState.Inactive, response.State);
    }

    [Fact]
    public async Task PatchAsync_WhenInUseAndBrandChanges_Throws()
    {
        var device = GivenStoredDevice(DeviceState.InUse);

        await Assert.ThrowsAsync<BusinessRuleViolationException>(
            () => _sut.PatchAsync(device.Id, new PatchDeviceRequest { Brand = "Samsung" }, CancellationToken.None));

        await _repository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PatchAsync_WithNoFields_Throws()
    {
        var device = GivenStoredDevice();

        var ex = await Assert.ThrowsAsync<DomainValidationException>(
            () => _sut.PatchAsync(device.Id, new PatchDeviceRequest(), CancellationToken.None));

        Assert.Equal("body", ex.Field);
    }

    [Fact]
    public async Task PatchAsync_WhenMissing_ThrowsNotFound()
    {
        var id = GivenMissingDevice();

        await Assert.ThrowsAsync<DeviceNotFoundException>(
            () => _sut.PatchAsync(id, new PatchDeviceRequest { Name = "X" }, CancellationToken.None));
    }

    // ---- Delete ----

    [Theory]
    [InlineData(DeviceState.Available)]
    [InlineData(DeviceState.Inactive)]
    public async Task DeleteAsync_WhenNotInUse_RemovesAndSaves(DeviceState state)
    {
        var device = GivenStoredDevice(state);

        await _sut.DeleteAsync(device.Id, CancellationToken.None);

        _repository.Received(1).Remove(device);
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAsync_WhenInUse_ThrowsAndDoesNotRemove()
    {
        var device = GivenStoredDevice(DeviceState.InUse);

        await Assert.ThrowsAsync<BusinessRuleViolationException>(
            () => _sut.DeleteAsync(device.Id, CancellationToken.None));

        _repository.DidNotReceive().Remove(Arg.Any<Device>());
        await _repository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAsync_WhenMissing_ThrowsNotFound()
    {
        var id = GivenMissingDevice();

        await Assert.ThrowsAsync<DeviceNotFoundException>(() => _sut.DeleteAsync(id, CancellationToken.None));

        _repository.DidNotReceive().Remove(Arg.Any<Device>());
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
