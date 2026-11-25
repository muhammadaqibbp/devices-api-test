using DevicesApi.Application;
using Microsoft.AspNetCore.Mvc;

namespace DevicesApi.Api;

[ApiController]
[Route("api/devices")]
[Produces("application/json")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
public sealed class DevicesController(IDeviceService deviceService) : ControllerBase
{
    /// <summary>Creates a new device.</summary>
    /// <remarks>The state defaults to <c>available</c>. The creation time is assigned by the server.</remarks>
    [HttpPost]
    [ProducesResponseType(typeof(DeviceResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<DeviceResponse>> Create(
        [FromBody] CreateDeviceRequest request, CancellationToken cancellationToken)
    {
        var device = await deviceService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = device.Id }, device);
    }

    /// <summary>Fetches a single device.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(DeviceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DeviceResponse>> GetById(Guid id, CancellationToken cancellationToken) =>
        Ok(await deviceService.GetByIdAsync(id, cancellationToken));

    /// <summary>Fetches devices, optionally filtered by brand and/or state.</summary>
    /// <param name="brand">Case-insensitive exact brand match.</param>
    /// <param name="state">One of <c>available</c>, <c>in-use</c>, <c>inactive</c>.</param>
    /// <param name="page">1-based page number.</param>
    /// <param name="pageSize">Items per page (1-100).</param>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<DeviceResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResponse<DeviceResponse>>> List(
        [FromQuery] string? brand,
        [FromQuery] string? state,
        CancellationToken cancellationToken,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var query = new DeviceQuery(brand, DeviceStateParser.Parse(state), page, pageSize);
        return Ok(await deviceService.ListAsync(query, cancellationToken));
    }

    /// <summary>Fully updates a device (name, brand and state are all required).</summary>
    /// <remarks>Name and brand cannot be changed while the device is in use. The creation time can never be changed.</remarks>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(DeviceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DeviceResponse>> Replace(
        Guid id, [FromBody] ReplaceDeviceRequest request, CancellationToken cancellationToken) =>
        Ok(await deviceService.ReplaceAsync(id, request, cancellationToken));

    /// <summary>Partially updates a device. Omitted fields are left unchanged.</summary>
    /// <remarks>Name and brand cannot be changed while the device is in use. The creation time can never be changed.</remarks>
    [HttpPatch("{id:guid}")]
    [ProducesResponseType(typeof(DeviceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DeviceResponse>> Patch(
        Guid id, [FromBody] PatchDeviceRequest request, CancellationToken cancellationToken) =>
        Ok(await deviceService.PatchAsync(id, request, cancellationToken));

    /// <summary>Deletes a device. Devices that are in use cannot be deleted.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await deviceService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
