using System.ComponentModel.DataAnnotations;
using PermitToWork.Application.Common;
using PermitToWork.Domain.Organization;

namespace PermitToWork.Application.Teams;

#region Read models

public sealed record TeamSummaryDto(
    Guid Id,
    string Code,
    string Name,
    Guid FacilityId,
    string FacilityName,
    TeamStatus Status,
    int ActiveMemberCount,
    string? LeaderName);

public sealed record TeamMemberDto(
    Guid MembershipId,
    Guid EmployeeId,
    string EmployeeNumber,
    string FirstName,
    string LastName,
    string TradeName,
    string CompanyName,
    TeamRole Role,
    DateOnly JoinedOn,
    DateOnly? LeftOn)
{
    public string FullName => $"{FirstName} {LastName}";

    /// <summary>Null means still a member — the same rule the domain uses.</summary>
    public bool IsCurrent => LeftOn is null;
}

public sealed record TeamDetailDto(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    Guid FacilityId,
    string FacilityName,
    TeamStatus Status,
    IReadOnlyList<TeamMemberDto> Members)
{
    /// <summary>
    /// Members carries the full history, including people who have left, because who was
    /// on the crew last March is exactly the question an incident investigation asks.
    /// Callers wanting today's roster filter on <see cref="TeamMemberDto.IsCurrent"/>.
    /// </summary>
    public int ActiveMemberCount => Members.Count(m => m.IsCurrent);

    public string? LeaderName => Members.FirstOrDefault(m => m.IsCurrent && m.Role == TeamRole.Leader)?.FullName;
}

#endregion

#region Requests

public sealed record TeamSearchRequest : PageRequest
{
    [StringLength(100)]
    public string? Search { get; init; }

    public Guid? FacilityId { get; init; }

    public TeamStatus? Status { get; init; }
}

public sealed record CreateTeamRequest
{
    // No code: it is generated as <first three letters>-<year>-<sequence>, e.g. MEC-2026-0001.

    [Required, StringLength(150)]
    public string Name { get; init; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; init; }

    [Required]
    public Guid FacilityId { get; init; }

    /// <summary>
    /// Required. A team is created together with the person who leads it, so a team with
    /// no members cannot exist — which matters because an empty team would be invisible to
    /// the very contractor who just created it.
    /// </summary>
    [Required]
    public Guid LeaderEmployeeId { get; init; }
}

public sealed record UpdateTeamRequest
{
    [Required, StringLength(150)]
    public string Name { get; init; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; init; }
}

public sealed record AddTeamMemberRequest
{
    [Required]
    public Guid EmployeeId { get; init; }

    public TeamRole Role { get; init; } = TeamRole.Member;

    /// <summary>Defaults to today.</summary>
    public DateOnly? JoinedOn { get; init; }
}

public sealed record ChangeMemberRoleRequest
{
    [Required]
    public TeamRole Role { get; init; }
}

public sealed record RemoveTeamMemberRequest
{
    /// <summary>Defaults to today. The first day they are no longer a member.</summary>
    public DateOnly? LeftOn { get; init; }
}

#endregion
