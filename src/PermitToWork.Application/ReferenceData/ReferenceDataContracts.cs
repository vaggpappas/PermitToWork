using System.ComponentModel.DataAnnotations;
using PermitToWork.Domain.Organization;

namespace PermitToWork.Application.ReferenceData;

// Requests for the administration screens. Codes are settable on creation and never
// afterwards — they are embedded in badge numbers, team codes and permit numbers already
// issued, so changing one would rewrite what those identifiers mean.

/// <summary>The shape most reference tables share: a code and a name.</summary>
public record CreateLookupRequest
{
    [Required, StringLength(20, MinimumLength = 2)]
    public string Code { get; init; } = string.Empty;

    [Required, StringLength(100)]
    public string Name { get; init; } = string.Empty;
}

public record RenameLookupRequest
{
    [Required, StringLength(100)]
    public string Name { get; init; } = string.Empty;
}

public sealed record CreateCompanyRequest : CreateLookupRequest
{
    /// <summary>Owner or Contractor. Decides whether their people see the whole site.</summary>
    [Required]
    public CompanyKind Kind { get; init; }
}

/// <summary>Facilities, buildings, locations and permit types all carry a description.</summary>
public sealed record CreatePlaceRequest
{
    [Required, StringLength(20, MinimumLength = 2)]
    public string Code { get; init; } = string.Empty;

    [Required, StringLength(200)]
    public string Name { get; init; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; init; }
}

public sealed record UpdatePlaceRequest
{
    [Required, StringLength(200)]
    public string Name { get; init; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; init; }
}

public sealed record CreatePermitTypeRequest
{
    /// <summary>Two to four letters. Becomes the prefix of every permit number of this type.</summary>
    [Required, StringLength(4, MinimumLength = 2)]
    public string Code { get; init; } = string.Empty;

    [Required, StringLength(100)]
    public string Name { get; init; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; init; }

    /// <summary>Certifications every worker on a permit of this type must hold.</summary>
    public IReadOnlyList<Guid> RequiredCertificationTypeIds { get; init; } = [];
}

public sealed record UpdatePermitTypeRequest
{
    [Required, StringLength(100)]
    public string Name { get; init; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; init; }

    public IReadOnlyList<Guid> RequiredCertificationTypeIds { get; init; } = [];
}

/// <summary>Reference rows as the administration screen shows them, including inactive ones.</summary>
public sealed record ReferenceItemDto(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    bool IsActive,
    Guid? ParentId,
    string? Extra);
