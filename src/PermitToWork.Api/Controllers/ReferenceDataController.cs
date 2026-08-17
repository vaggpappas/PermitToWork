using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PermitToWork.Application.Abstractions;
using PermitToWork.Application.ReferenceData;
using PermitToWork.Infrastructure.Identity;

namespace PermitToWork.Api.Controllers;

/// <summary>
/// Administration of the reference tables — companies, the facility hierarchy, trades,
/// certification types, categories and permit types.
/// <para>
/// Administrators only, all of it. These rows decide which certifications a permit demands
/// and which companies exist to be scoped by, so somebody who can edit them can change what
/// the safety rules are. Reading them stays open on <c>/api/lookups</c>, where every screen
/// fills its dropdowns.
/// </para>
/// <para>
/// Nothing here deletes. Permits, badge numbers and team codes already point at these rows,
/// so retiring is a flag and the history stays readable.
/// </para>
/// </summary>
[ApiController]
[Route("api/admin")]
[Authorize(Roles = ApplicationRoles.Administrator)]
[Produces("application/json")]
public sealed class ReferenceDataController(
    IReferenceDataService reference,
    IReferenceDataRepository referenceData) : ControllerBase
{
    /// <summary>
    /// Every row of one reference table, including retired ones — which is the difference
    /// between this and <c>/api/lookups</c>. An administrator has to see what they retired
    /// in order to restore it.
    /// </summary>
    [HttpGet("{kind}")]
    [ProducesResponseType<IReadOnlyList<ReferenceItemDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ReferenceItemDto>>> List(
        ReferenceKind kind,
        [FromQuery] Guid? parentId,
        CancellationToken cancellationToken) =>
        Ok(await referenceData.ListForAdminAsync(kind, parentId, cancellationToken));

    #region Companies

    [HttpPost("companies")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateCompany(CreateCompanyRequest request, CancellationToken cancellationToken) =>
        Created(string.Empty, new { id = await reference.CreateCompanyAsync(request, cancellationToken) });

    [HttpPut("companies/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RenameCompany(
        Guid id,
        RenameLookupRequest request,
        CancellationToken cancellationToken)
    {
        await reference.RenameCompanyAsync(id, request, cancellationToken);
        return NoContent();
    }

    #endregion

    #region Facilities, buildings and locations

    [HttpPost("facilities")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateFacility(CreatePlaceRequest request, CancellationToken cancellationToken) =>
        Created(string.Empty, new { id = await reference.CreateFacilityAsync(request, cancellationToken) });

    [HttpPut("facilities/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> UpdateFacility(
        Guid id,
        UpdatePlaceRequest request,
        CancellationToken cancellationToken)
    {
        await reference.UpdateFacilityAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpPost("facilities/{facilityId:guid}/buildings")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateBuilding(
        Guid facilityId,
        CreatePlaceRequest request,
        CancellationToken cancellationToken) =>
        Created(string.Empty, new { id = await reference.CreateBuildingAsync(facilityId, request, cancellationToken) });

    [HttpPut("buildings/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> UpdateBuilding(
        Guid id,
        UpdatePlaceRequest request,
        CancellationToken cancellationToken)
    {
        await reference.UpdateBuildingAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpPost("buildings/{buildingId:guid}/locations")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateLocation(
        Guid buildingId,
        CreatePlaceRequest request,
        CancellationToken cancellationToken) =>
        Created(string.Empty, new { id = await reference.CreateLocationAsync(buildingId, request, cancellationToken) });

    [HttpPut("locations/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> UpdateLocation(
        Guid id,
        UpdatePlaceRequest request,
        CancellationToken cancellationToken)
    {
        await reference.UpdateLocationAsync(id, request, cancellationToken);
        return NoContent();
    }

    #endregion

    #region Trades, certification types and categories

    [HttpPost("trades")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateTrade(CreateLookupRequest request, CancellationToken cancellationToken) =>
        Created(string.Empty, new { id = await reference.CreateTradeAsync(request, cancellationToken) });

    [HttpPut("trades/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RenameTrade(
        Guid id,
        RenameLookupRequest request,
        CancellationToken cancellationToken)
    {
        await reference.RenameTradeAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpPost("certification-types")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateCertificationType(
        CreateLookupRequest request,
        CancellationToken cancellationToken) =>
        Created(string.Empty, new { id = await reference.CreateCertificationTypeAsync(request, cancellationToken) });

    [HttpPut("certification-types/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RenameCertificationType(
        Guid id,
        RenameLookupRequest request,
        CancellationToken cancellationToken)
    {
        await reference.RenameCertificationTypeAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpPost("categories")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateCategory(CreateLookupRequest request, CancellationToken cancellationToken) =>
        Created(string.Empty, new { id = await reference.CreateCategoryAsync(request, cancellationToken) });

    [HttpPut("categories/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RenameCategory(
        Guid id,
        RenameLookupRequest request,
        CancellationToken cancellationToken)
    {
        await reference.RenameCategoryAsync(id, request, cancellationToken);
        return NoContent();
    }

    #endregion

    #region Permit types

    /// <summary>
    /// Creates a permit type and the certifications it demands. The code becomes the prefix
    /// of every permit number of this type, so it cannot be changed afterwards.
    /// </summary>
    [HttpPost("permit-types")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<IActionResult> CreatePermitType(
        CreatePermitTypeRequest request,
        CancellationToken cancellationToken) =>
        Created(string.Empty, new { id = await reference.CreatePermitTypeAsync(request, cancellationToken) });

    /// <summary>
    /// Changes what a permit type demands from now on. Permits already raised keep the
    /// requirements they were raised under — those were copied onto them at the time.
    /// </summary>
    [HttpPut("permit-types/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> UpdatePermitType(
        Guid id,
        UpdatePermitTypeRequest request,
        CancellationToken cancellationToken)
    {
        await reference.UpdatePermitTypeAsync(id, request, cancellationToken);
        return NoContent();
    }

    #endregion

    /// <summary>
    /// Retires or restores a reference row. There is no delete: permits and badge numbers
    /// already point at these, and removing one would leave records referring to nothing.
    /// </summary>
    [HttpPut("{kind}/{id:guid}/active")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SetActive(
        ReferenceKind kind,
        Guid id,
        [FromBody] SetActiveRequest request,
        CancellationToken cancellationToken)
    {
        await reference.SetActiveAsync(kind, id, request.IsActive, cancellationToken);
        return NoContent();
    }
}

public sealed record SetActiveRequest(bool IsActive);
