using PermitToWork.Domain.Common;

namespace PermitToWork.Domain.Organization;

/// <summary>
/// A qualification held by one employee, valid for a period.
/// <para>
/// Part of the <see cref="Employee"/> aggregate: the constructor is <c>internal</c> so a
/// certification can only come into existence through
/// <see cref="Employee.AddCertification"/>. The aggregate boundary is enforced by the
/// compiler rather than by a comment asking people to be careful.
/// </para>
/// </summary>
public class Certification : Entity
{
    private Certification() { }

    internal Certification(
        Guid employeeId,
        Guid certificationTypeId,
        string issuedBy,
        DateOnly issuedOn,
        DateOnly expiresOn,
        string? referenceNumber)
    {
        if (expiresOn <= issuedOn)
        {
            throw new DomainException("A certification must expire after the date it was issued.");
        }

        EmployeeId = Guard.Required(employeeId, "Employee");
        CertificationTypeId = Guard.Required(certificationTypeId, "Certification type");
        IssuedBy = Guard.Required(issuedBy, "Issuing body", 200);
        IssuedOn = issuedOn;
        ExpiresOn = expiresOn;
        ReferenceNumber = Guard.Optional(referenceNumber, "Reference number", 50);
    }

    public Guid EmployeeId { get; private set; }
    public Guid CertificationTypeId { get; private set; }
    public string IssuedBy { get; private set; } = null!;
    public DateOnly IssuedOn { get; private set; }
    public DateOnly ExpiresOn { get; private set; }
    public string? ReferenceNumber { get; private set; }

    /// <summary>
    /// Validity is computed from the dates, never stored. A stored "is valid" flag is
    /// correct only until midnight on the expiry date and wrong every day after.
    /// </summary>
    public bool IsValidOn(DateOnly asOf) => asOf >= IssuedOn && asOf <= ExpiresOn;
}
