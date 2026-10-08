using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GIBFramework.Services.Locations;

namespace GIBFramework.Controllers;

[ApiController]
[Route("api/v1/locations")]
[Authorize]
public sealed class LocationsController(LocationService locations) : ControllerBase
{
    [HttpGet("provinces")]
    public async Task<IReadOnlyList<LocationItem>> Provinces(CancellationToken ct) => await locations.ProvincesAsync(ct);

    [HttpGet("provinces/{provinceId:int}/districts")]
    public async Task<LocationList> Districts(int provinceId, CancellationToken ct) => await locations.DistrictsAsync(provinceId, ct);

    [HttpGet("districts/{districtId:int}/neighborhoods")]
    public async Task<LocationList> Neighborhoods(int districtId, CancellationToken ct) => await locations.NeighborhoodsAsync(districtId, ct);

    [HttpGet("status")]
    public async Task<LocationStatus> Status(CancellationToken ct) => await locations.StatusAsync(ct);
}
