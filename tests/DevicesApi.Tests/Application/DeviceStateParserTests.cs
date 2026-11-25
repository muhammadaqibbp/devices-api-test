using DevicesApi.Application;
using DevicesApi.Domain;

namespace DevicesApi.Tests.Application;

public class DeviceStateParserTests
{
    [Theory]
    [InlineData("available", DeviceState.Available)]
    [InlineData("AVAILABLE", DeviceState.Available)]
    [InlineData("in-use", DeviceState.InUse)]
    [InlineData("In-Use", DeviceState.InUse)]
    [InlineData("in_use", DeviceState.InUse)]
    [InlineData("inuse", DeviceState.InUse)]
    [InlineData("in use", DeviceState.InUse)]
    [InlineData("inactive", DeviceState.Inactive)]
    public void Parse_WithKnownValue_ReturnsState(string input, DeviceState expected)
    {
        Assert.Equal(expected, DeviceStateParser.Parse(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_WithBlank_ReturnsNull(string? input)
    {
        Assert.Null(DeviceStateParser.Parse(input));
    }

    [Theory]
    [InlineData("broken")]
    [InlineData("2")]
    [InlineData("99")]
    [InlineData("-")]
    public void Parse_WithUnknownValue_Throws(string input)
    {
        var ex = Assert.Throws<DomainValidationException>(() => DeviceStateParser.Parse(input));

        Assert.Equal("state", ex.Field);
    }
}
