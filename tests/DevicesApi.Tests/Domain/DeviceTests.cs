using DevicesApi.Domain;
using Xunit;

namespace DevicesApi.Tests.Domain;

public class DeviceTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 15, 10, 0, 0, TimeSpan.Zero);

    private static Device NewDevice(DeviceState state = DeviceState.Available) =>
        Device.Create("iPhone 15", "Apple", state, Now);

    // ---- Create ----

    [Fact]
    public void Create_WithValidData_SetsAllProperties()
    {
        var device = Device.Create("iPhone 15", "Apple", DeviceState.Available, Now);

        Assert.NotEqual(Guid.Empty, device.Id);
        Assert.Equal("iPhone 15", device.Name);
        Assert.Equal("Apple", device.Brand);
        Assert.Equal(DeviceState.Available, device.State);
        Assert.Equal(Now, device.CreatedAt);
    }

    [Fact]
    public void Create_TrimsNameAndBrand()
    {
        var device = Device.Create("  Pixel 9  ", "  Google ", DeviceState.Inactive, Now);

        Assert.Equal("Pixel 9", device.Name);
        Assert.Equal("Google", device.Brand);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithBlankName_Throws(string? name)
    {
        var ex = Assert.Throws<DomainValidationException>(
            () => Device.Create(name, "Apple", DeviceState.Available, Now));

        Assert.Equal("name", ex.Field);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithBlankBrand_Throws(string? brand)
    {
        var ex = Assert.Throws<DomainValidationException>(
            () => Device.Create("iPhone", brand, DeviceState.Available, Now));

        Assert.Equal("brand", ex.Field);
    }

    [Fact]
    public void Create_WithNameAtMaxLength_Succeeds()
    {
        var device = Device.Create(new string('a', Device.NameMaxLength), "Apple", DeviceState.Available, Now);

        Assert.Equal(Device.NameMaxLength, device.Name.Length);
    }

    [Fact]
    public void Create_WithNameOverMaxLength_Throws()
    {
        var ex = Assert.Throws<DomainValidationException>(
            () => Device.Create(new string('a', Device.NameMaxLength + 1), "Apple", DeviceState.Available, Now));

        Assert.Equal("name", ex.Field);
    }

    [Fact]
    public void Create_WithBrandOverMaxLength_Throws()
    {
        var ex = Assert.Throws<DomainValidationException>(
            () => Device.Create("iPhone", new string('b', Device.BrandMaxLength + 1), DeviceState.Available, Now));

        Assert.Equal("brand", ex.Field);
    }

    [Fact]
    public void Create_WithUndefinedState_Throws()
    {
        var ex = Assert.Throws<DomainValidationException>(
            () => Device.Create("iPhone", "Apple", (DeviceState)99, Now));

        Assert.Equal("state", ex.Field);
    }

    // ---- Update: allowed changes ----

    [Fact]
    public void Update_WhenAvailable_ChangesNameBrandAndState()
    {
        var device = NewDevice(DeviceState.Available);

        device.Update("Galaxy S24", "Samsung", DeviceState.Inactive);

        Assert.Equal("Galaxy S24", device.Name);
        Assert.Equal("Samsung", device.Brand);
        Assert.Equal(DeviceState.Inactive, device.State);
    }

    [Fact]
    public void Update_WhenInactive_ChangesName()
    {
        var device = NewDevice(DeviceState.Inactive);

        device.Update("iPhone 16", null, null);

        Assert.Equal("iPhone 16", device.Name);
    }

    [Fact]
    public void Update_WithOnlyNameSupplied_LeavesOtherFieldsUnchanged()
    {
        var device = NewDevice(DeviceState.Available);

        device.Update("iPhone 16", null, null);

        Assert.Equal("Apple", device.Brand);
        Assert.Equal(DeviceState.Available, device.State);
    }

    [Fact]
    public void Update_WithNoValues_ChangesNothing()
    {
        var device = NewDevice();

        device.Update(null, null, null);

        Assert.Equal("iPhone 15", device.Name);
        Assert.Equal("Apple", device.Brand);
        Assert.Equal(DeviceState.Available, device.State);
        Assert.Equal(0, device.Version);
    }

    [Fact]
    public void Update_DoesNotChangeCreationTimeOrId()
    {
        var device = NewDevice();
        var id = device.Id;

        device.Update("Other", "Other", DeviceState.Inactive);

        Assert.Equal(Now, device.CreatedAt);
        Assert.Equal(id, device.Id);
    }

    // ---- Update: in-use rule ----

    [Fact]
    public void Update_WhenInUse_ChangingName_Throws()
    {
        var device = NewDevice(DeviceState.InUse);

        Assert.Throws<BusinessRuleViolationException>(() => device.Update("New name", null, null));
        Assert.Equal("iPhone 15", device.Name);
    }

    [Fact]
    public void Update_WhenInUse_ChangingBrand_Throws()
    {
        var device = NewDevice(DeviceState.InUse);

        Assert.Throws<BusinessRuleViolationException>(() => device.Update(null, "Samsung", null));
        Assert.Equal("Apple", device.Brand);
    }

    [Fact]
    public void Update_WhenInUse_ChangingNameAndState_ThrowsAndAppliesNothing()
    {
        var device = NewDevice(DeviceState.InUse);

        Assert.Throws<BusinessRuleViolationException>(
            () => device.Update("New name", null, DeviceState.Available));

        Assert.Equal("iPhone 15", device.Name);
        Assert.Equal(DeviceState.InUse, device.State);
    }

    [Fact]
    public void Update_WhenInUse_ChangingOnlyState_Succeeds()
    {
        var device = NewDevice(DeviceState.InUse);

        device.Update(null, null, DeviceState.Available);

        Assert.Equal(DeviceState.Available, device.State);
    }

    [Fact]
    public void Update_WhenInUse_ResendingSameNameAndBrand_Succeeds()
    {
        var device = NewDevice(DeviceState.InUse);

        device.Update("iPhone 15", "Apple", DeviceState.Inactive);

        Assert.Equal(DeviceState.Inactive, device.State);
    }

    // ---- Update: validation ----

    [Fact]
    public void Update_WithBlankName_Throws()
    {
        var device = NewDevice();

        var ex = Assert.Throws<DomainValidationException>(() => device.Update("  ", null, null));

        Assert.Equal("name", ex.Field);
    }

    [Fact]
    public void Update_WithUndefinedState_Throws()
    {
        var device = NewDevice();

        Assert.Throws<DomainValidationException>(() => device.Update(null, null, (DeviceState)42));
    }

    // ---- Version ----

    [Fact]
    public void Update_WithEffectiveChange_IncrementsVersion()
    {
        var device = NewDevice();

        device.Update("Other", null, null);

        Assert.Equal(1, device.Version);
    }

    [Fact]
    public void Update_WithIdenticalValues_DoesNotIncrementVersion()
    {
        var device = NewDevice();

        device.Update("iPhone 15", "Apple", DeviceState.Available);

        Assert.Equal(0, device.Version);
    }

    // ---- Delete rule ----

    [Fact]
    public void EnsureCanBeDeleted_WhenInUse_Throws()
    {
        var device = NewDevice(DeviceState.InUse);

        Assert.Throws<BusinessRuleViolationException>(device.EnsureCanBeDeleted);
    }

    [Theory]
    [InlineData(DeviceState.Available)]
    [InlineData(DeviceState.Inactive)]
    public void EnsureCanBeDeleted_WhenNotInUse_DoesNotThrow(DeviceState state)
    {
        var device = NewDevice(state);

        var exception = Record.Exception(device.EnsureCanBeDeleted);

        Assert.Null(exception);
    }
}
