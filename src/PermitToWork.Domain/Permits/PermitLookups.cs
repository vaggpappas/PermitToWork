using PermitToWork.Domain.Common;

namespace PermitToWork.Domain.Permits;

/// <summary>
/// A kind of hazardous work — Hot Work, Confined Space Entry, Working at Height.
/// <para>
/// Carries the certifications it requires, which is where the hard block comes from, and
/// the two-to-four letter code that begins every permit number of that type.
/// </para>
/// </summary>
public class PermitType : Entity
{
    private readonly List<PermitTypeCertification> _requiredCertifications = [];

    private PermitType() { }

    public PermitType(string code, string name, string? description = null)
    {
        Code = Guard.Required(code, "Permit type code", 4).ToUpperInvariant();
        Name = Guard.Required(name, "Permit type name", 100);
        Description = Guard.Optional(description, "Description", 500);

        if (Code.Length < 2)
        {
            throw new DomainException("A permit type code must be two to four letters.");
        }
    }

    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }
    public bool IsActive { get; private set; } = true;

    /// <summary>What every worker on a permit of this type must hold.</summary>
    public IReadOnlyList<PermitTypeCertification> RequiredCertifications => _requiredCertifications;

    public void RequireCertification(Guid certificationTypeId)
    {
        Guard.Required(certificationTypeId, "Certification type");

        if (_requiredCertifications.All(r => r.CertificationTypeId != certificationTypeId))
        {
            _requiredCertifications.Add(new PermitTypeCertification(Id, certificationTypeId));
        }
    }

    public void StopRequiringCertification(Guid certificationTypeId)
    {
        var requirement = _requiredCertifications
            .SingleOrDefault(r => r.CertificationTypeId == certificationTypeId);

        if (requirement is not null)
        {
            _requiredCertifications.Remove(requirement);
        }
    }

    public void Deactivate() => IsActive = false;

    public void Reactivate() => IsActive = true;
}

/// <summary>A certification a permit type demands of everyone who works under it.</summary>
public class PermitTypeCertification : Entity
{
    private PermitTypeCertification() { }

    internal PermitTypeCertification(Guid permitTypeId, Guid certificationTypeId)
    {
        PermitTypeId = permitTypeId;
        CertificationTypeId = Guard.Required(certificationTypeId, "Certification type");
    }

    public Guid PermitTypeId { get; private set; }
    public Guid CertificationTypeId { get; private set; }
}

/// <summary>Why the work is happening — Maintenance, Inspection, Construction, Cleaning.</summary>
public class TaskGroup : Entity
{
    private TaskGroup() { }

    public TaskGroup(string code, string name)
    {
        Code = Guard.Required(code, "Task group code", 20).ToUpperInvariant();
        Name = Guard.Required(name, "Task group name", 100);
    }

    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public bool IsActive { get; private set; } = true;

    public void Deactivate() => IsActive = false;
}
