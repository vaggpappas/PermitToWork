using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PermitToWork.Application.Common;
using PermitToWork.Application.Employees;
using PermitToWork.Infrastructure.Identity;

namespace PermitToWork.Api.Controllers;

/// <summary>
/// Employee records. Every read here is already limited to the caller's company by the
/// query filter on the DbContext — there is no company check in this file because there
/// cannot be a route that forgets one.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public sealed class EmployeesController(IEmployeeService employees) : ControllerBase
{
    /// <summary>Searches employees by name, badge number or email.</summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<EmployeeSummaryDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<EmployeeSummaryDto>>> Search(
        [FromQuery] EmployeeSearchRequest request,
        CancellationToken cancellationToken) =>
        Ok(await employees.SearchAsync(request, cancellationToken));

    /// <summary>One employee, with certifications and computed age.</summary>
    /// <summary>The signed-in user's own record, with their certifications.</summary>
    /// <remarks>
    /// Before <c>{id:guid}</c> in this file, but that is only tidiness — "me" is not a Guid,
    /// so the route constraint would never have matched it anyway.
    /// </remarks>
    [HttpGet("me")]
    [ProducesResponseType<EmployeeDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeDetailDto>> GetMyProfile(CancellationToken cancellationToken) =>
        Ok(await employees.GetMyProfileAsync(cancellationToken));

    /// <summary>
    /// Updates the signed-in user's own phone number and address.
    /// </summary>
    /// <remarks>
    /// No role attribute and no id in the route: any signed-in person may edit their own
    /// contact details and nobody else's. What they may change is limited by the shape of
    /// <see cref="UpdateMyContactRequest"/> rather than by a check in here — trade, job title
    /// and email have no field to travel in, so no amount of crafting the request body can
    /// reach them.
    /// </remarks>
    [HttpPut("me/contact")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateMyContact(
        UpdateMyContactRequest request,
        CancellationToken cancellationToken)
    {
        await employees.UpdateMyContactAsync(request, cancellationToken);
        return NoContent();
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<EmployeeDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeDetailDto>> Get(Guid id, CancellationToken cancellationToken) =>
        Ok(await employees.GetAsync(id, cancellationToken));

    /// <summary>
    /// Creates an employee record. Administrators only — the badge number and employer on
    /// this record are what every later authorisation decision is built on, so they are
    /// not something a person may assert about themselves.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = ApplicationRoles.Administrator)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(CreateEmployeeRequest request, CancellationToken cancellationToken)
    {
        var id = await employees.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id }, new { id });
    }

    /// <summary>
    /// Sets what this person may do. Supervisors and administrators only — this is the
    /// field that grants every other permission, so it is itself the most restricted one.
    /// </summary>
    /// <response code="422">They have been terminated, and cannot hold a role.</response>
    [HttpPut("{id:guid}/access-role")]
    [Authorize(Roles = $"{ApplicationRoles.Administrator},{ApplicationRoles.Supervisor}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> AssignAccessRole(
        Guid id,
        AssignAccessRoleRequest request,
        CancellationToken cancellationToken)
    {
        await employees.AssignAccessRoleAsync(id, request.AccessRole, cancellationToken);
        return NoContent();
    }

    /// <summary>Updates the profile fields a person is allowed to change.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = $"{ApplicationRoles.Administrator},{ApplicationRoles.Supervisor}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, UpdateEmployeeRequest request, CancellationToken cancellationToken)
    {
        await employees.UpdateAsync(id, request, cancellationToken);
        return NoContent();
    }

    /// <summary>Sets or clears who this employee reports to. Pass null to clear.</summary>
    [HttpPut("{id:guid}/manager")]
    [Authorize(Roles = $"{ApplicationRoles.Administrator},{ApplicationRoles.Supervisor}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> AssignManager(
        Guid id,
        [FromBody] AssignManagerRequest request,
        CancellationToken cancellationToken)
    {
        await employees.AssignManagerAsync(id, request.ManagerId, cancellationToken);
        return NoContent();
    }

    /// <summary>Suspends an active employee.</summary>
    /// <response code="422">They are not currently active.</response>
    [HttpPost("{id:guid}/suspend")]
    [Authorize(Roles = ApplicationRoles.Administrator)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public Task<IActionResult> Suspend(Guid id, CancellationToken cancellationToken) =>
        ChangeStatus(id, EmploymentAction.Suspend, cancellationToken);

    /// <summary>Returns a suspended employee to active duty.</summary>
    /// <response code="422">They are not currently suspended.</response>
    [HttpPost("{id:guid}/reinstate")]
    [Authorize(Roles = ApplicationRoles.Administrator)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public Task<IActionResult> Reinstate(Guid id, CancellationToken cancellationToken) =>
        ChangeStatus(id, EmploymentAction.Reinstate, cancellationToken);

    /// <summary>Ends employment. There is no delete — the history has to survive.</summary>
    [HttpPost("{id:guid}/terminate")]
    [Authorize(Roles = ApplicationRoles.Administrator)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public Task<IActionResult> Terminate(Guid id, CancellationToken cancellationToken) =>
        ChangeStatus(id, EmploymentAction.Terminate, cancellationToken);

    /// <summary>Records a qualification. Renewals are added, not overwritten.</summary>
    [HttpPost("{id:guid}/certifications")]
    [Authorize(Roles = $"{ApplicationRoles.Administrator},{ApplicationRoles.SafetyOfficer}")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> AddCertification(
        Guid id,
        AddCertificationRequest request,
        CancellationToken cancellationToken)
    {
        var certificationId = await employees.AddCertificationAsync(id, request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id }, new { id = certificationId });
    }

    /// <summary>Removes a certification recorded in error.</summary>
    [HttpDelete("{id:guid}/certifications/{certificationId:guid}")]
    [Authorize(Roles = $"{ApplicationRoles.Administrator},{ApplicationRoles.SafetyOfficer}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RemoveCertification(
        Guid id,
        Guid certificationId,
        CancellationToken cancellationToken)
    {
        await employees.RemoveCertificationAsync(id, certificationId, cancellationToken);
        return NoContent();
    }

    private async Task<IActionResult> ChangeStatus(Guid id, EmploymentAction action, CancellationToken cancellationToken)
    {
        await employees.ChangeStatusAsync(id, action, cancellationToken);
        return NoContent();
    }
}

/// <summary>Null clears the reporting line.</summary>
public sealed record AssignManagerRequest(Guid? ManagerId);
