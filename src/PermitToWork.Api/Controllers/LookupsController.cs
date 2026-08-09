using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PermitToWork.Application.Abstractions;

namespace PermitToWork.Api.Controllers;

/// <summary>
/// Reference data for filling dropdowns: companies, trades, certification types and the
/// facility → building → location hierarchy. Read-only; administrators manage the
/// underlying rows.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public sealed class LookupsController(IReferenceDataRepository referenceData) : ControllerBase
{
    [HttpGet("companies")]
    public async Task<ActionResult<IReadOnlyList<LookupDto>>> Companies(CancellationToken cancellationToken) =>
        Ok(await referenceData.GetCompaniesAsync(cancellationToken));

    [HttpGet("trades")]
    public async Task<ActionResult<IReadOnlyList<LookupDto>>> Trades(CancellationToken cancellationToken) =>
        Ok(await referenceData.GetTradesAsync(cancellationToken));

    [HttpGet("certification-types")]
    public async Task<ActionResult<IReadOnlyList<LookupDto>>> CertificationTypes(CancellationToken cancellationToken) =>
        Ok(await referenceData.GetCertificationTypesAsync(cancellationToken));

    [HttpGet("facilities")]
    public async Task<ActionResult<IReadOnlyList<LookupDto>>> Facilities(CancellationToken cancellationToken) =>
        Ok(await referenceData.GetFacilitiesAsync(cancellationToken));

    [HttpGet("facilities/{facilityId:guid}/buildings")]
    public async Task<ActionResult<IReadOnlyList<LookupDto>>> Buildings(
        Guid facilityId,
        CancellationToken cancellationToken) =>
        Ok(await referenceData.GetBuildingsAsync(facilityId, cancellationToken));

    [HttpGet("buildings/{buildingId:guid}/locations")]
    public async Task<ActionResult<IReadOnlyList<LookupDto>>> Locations(
        Guid buildingId,
        CancellationToken cancellationToken) =>
        Ok(await referenceData.GetLocationsAsync(buildingId, cancellationToken));
}
