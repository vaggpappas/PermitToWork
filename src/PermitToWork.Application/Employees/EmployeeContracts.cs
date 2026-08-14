using System.ComponentModel.DataAnnotations;
using PermitToWork.Application.Common;
using PermitToWork.Domain.Organization;

namespace PermitToWork.Application.Employees;

// Read models and request models.
//
// The read models are projected straight from the database by the repository — they are
// not mapped from loaded entities. That is deliberate: a list of 50 employees needs the
// trade and company *names*, and Employee holds only ids, because an aggregate refers to
// other aggregates by identity rather than by object reference. Loading four aggregates
// per row to read two strings would be the wrong trade. So: queries return read models,
// commands load aggregates.

#region Read models

public sealed record EmployeeSummaryDto(
    Guid Id,
    string EmployeeNumber,
    string FirstName,
    string LastName,
    string Email,
    string JobTitle,
    string TradeName,
    string CompanyName,
    EmploymentStatus Status,
    AccessRole AccessRole,
    bool HasAccount)
{
    public string FullName => $"{FirstName} {LastName}";
}

public sealed record AddressDto(string Street, string City, string PostalCode, string Country);

public sealed record CertificationDto(
    Guid Id,
    Guid CertificationTypeId,
    string CertificationTypeName,
    string IssuedBy,
    DateOnly IssuedOn,
    DateOnly ExpiresOn,
    string? ReferenceNumber)
{
    /// <summary>Computed here rather than stored, for the same reason the domain computes it.</summary>
    public bool IsValid => DateOnly.FromDateTime(DateTime.UtcNow) is var today
                           && today >= IssuedOn && today <= ExpiresOn;
}

public sealed record EmployeeDetailDto(
    Guid Id,
    string EmployeeNumber,
    string FirstName,
    string LastName,
    string Email,
    string? PhoneNumber,
    AddressDto? Address,
    DateOnly? DateOfBirth,
    string JobTitle,
    Guid TradeId,
    string TradeName,
    Guid CompanyId,
    string CompanyName,
    Guid? ManagerId,
    string? ManagerName,
    DateOnly HireDate,
    EmploymentStatus Status,
    AccessRole AccessRole,
    bool HasAccount,
    IReadOnlyList<CertificationDto> Certifications)
{
    public string FullName => $"{FirstName} {LastName}";

    /// <summary>
    /// Derived from the date of birth on every read. The database stores when they were
    /// born, which stays true; it does not store how old they are, which does not.
    /// </summary>
    public int? Age
    {
        get
        {
            if (DateOfBirth is not { } dob)
            {
                return null;
            }

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var age = today.Year - dob.Year;
            return today < dob.AddYears(age) ? age - 1 : age;
        }
    }
}

#endregion

#region Requests

public sealed record EmployeeSearchRequest : PageRequest
{
    /// <summary>Matches against name, employee number and email.</summary>
    [StringLength(100)]
    public string? Search { get; init; }

    public Guid? CompanyId { get; init; }

    public Guid? TradeId { get; init; }

    public EmploymentStatus? Status { get; init; }
}

public sealed record CreateEmployeeRequest
{
    // No badge number: it is generated from the company code and a per-company sequence.
    // Leaving it off the request is what makes it impossible to set, rather than a rule
    // written down somewhere that a future endpoint forgets.

    [Required, StringLength(80)]
    public string FirstName { get; init; } = string.Empty;

    [Required, StringLength(80)]
    public string LastName { get; init; } = string.Empty;

    [Required, EmailAddress, StringLength(254)]
    public string Email { get; init; } = string.Empty;

    [StringLength(30)]
    public string? PhoneNumber { get; init; }

    [Required]
    public Guid CompanyId { get; init; }

    [Required]
    public Guid TradeId { get; init; }

    [Required, StringLength(120)]
    public string JobTitle { get; init; } = string.Empty;

    [Required]
    public DateOnly HireDate { get; init; }

    public DateOnly? DateOfBirth { get; init; }

    public Guid? ManagerId { get; init; }

    public AddressDto? Address { get; init; }
}

public sealed record UpdateEmployeeRequest
{
    [Required, StringLength(80)]
    public string FirstName { get; init; } = string.Empty;

    [Required, StringLength(80)]
    public string LastName { get; init; } = string.Empty;

    [Required, EmailAddress, StringLength(254)]
    public string Email { get; init; } = string.Empty;

    [StringLength(30)]
    public string? PhoneNumber { get; init; }

    [Required, StringLength(120)]
    public string JobTitle { get; init; } = string.Empty;

    [Required]
    public Guid TradeId { get; init; }

    public DateOnly? DateOfBirth { get; init; }

    public AddressDto? Address { get; init; }
}

public sealed record AddCertificationRequest
{
    [Required]
    public Guid CertificationTypeId { get; init; }

    [Required, StringLength(200)]
    public string IssuedBy { get; init; } = string.Empty;

    [Required]
    public DateOnly IssuedOn { get; init; }

    [Required]
    public DateOnly ExpiresOn { get; init; }

    [StringLength(50)]
    public string? ReferenceNumber { get; init; }
}

/// <summary>
/// The three ways employment status can change. An enum rather than three near-identical
/// service methods, so the transition table lives in one place and the domain decides
/// which moves are legal.
/// </summary>
public enum EmploymentAction
{
    Suspend = 1,
    Reinstate = 2,
    Terminate = 3
}

public sealed record AssignAccessRoleRequest
{
    [Required]
    public AccessRole AccessRole { get; init; }
}

#endregion
