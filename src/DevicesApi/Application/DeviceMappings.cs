using DevicesApi.Domain;

namespace DevicesApi.Application;

public static class DeviceMappings
{
    public static DeviceResponse ToResponse(this Device device) =>
        new(device.Id, device.Name, device.Brand, device.State, device.CreatedAt);
}
