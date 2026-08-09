using PermitToWork.Domain.Common;
using PermitToWork.Domain.ValueObjects;

namespace PermitToWork.Domain.Organization;

/// <summary>
/// Where an employee stands with their employer. An enum rather than an <c>IsActive</c>
/// bool because "suspended" and "terminated" are genuinely different: a suspended employee
/// can be reinstated and still shows in the org chart; a terminated one cannot.
/// </summary>
public enum EmploymentStatus
{
    Active = 1,
    Suspended = 2,
    Terminated = 3
}

/// <summary>
/// A person who works on site, employed by a <see cref="Company"/> that either owns the
/// site or contracts to it. Aggregate root; owns its <see cref="Certification"/>s.
/// </summary>
public class Employee : Entity
{
    private readonly List<Certification> _certifications = [];

    private Employee() { }

    public Employee(
        EmployeeNumber number,
        PersonName name,
        ContactInfo contact,
        Guid companyId,
        Guid tradeId,
        string jobTitle,
        DateOnly hireDate)
    {
        Number = number;
        Name = name;
        Contact = contact;
        CompanyId = Guard.Required(companyId, "Company");
        TradeId = Guard.Required(tradeId, "Trade");
        JobTitle = Guard.Required(jobTitle, "Job title", 120);
        HireDate = hireDate;
        Status = EmploymentStatus.Active;
    }

    #region Identity and profile

    public EmployeeNumber Number { get; private set; } = null!;
    public PersonName Name { get; private set; } = null!;
    public ContactInfo Contact { get; private set; } = null!;
    public Address? Address { get; private set; }
    public DateOnly? DateOfBirth { get; private set; }

    /// <summary>
    /// The org-chart title, e.g. "Senior Maintenance Engineer". Free text and purely
    /// descriptive — distinct from <see cref="TradeId"/>, which is the rule-bearing craft.
    /// Merging the two would mean parsing prose to make a safety decision.
    /// </summary>
    public string JobTitle { get; private set; } = null!;

    public Guid TradeId { get; private set; }
    public Guid CompanyId { get; private set; }

    /// <summary>Who this person reports to. Null at the top of the chain.</summary>
    public Guid? ManagerId { get; private set; }

    public DateOnly HireDate { get; private set; }
    public EmploymentStatus Status { get; private set; }

    /// <summary>
    /// The Identity account, once they have registered. Null for people an administrator
    /// entered before they ever logged in — which is the normal case for contractor crews.
    /// </summary>
    public Guid? UserId { get; private set; }

    /// <summary>
    /// Age is derived, never stored: a stored age is wrong the day after their birthday.
    /// Takes the reference date as a parameter rather than reading the system clock, so
    /// the rule is testable without freezing time.
    /// </summary>
    public int? AgeOn(DateOnly asOf)
    {
        if (DateOfBirth is not { } dateOfBirth)
        {
            return null;
        }

        var age = asOf.Year - dateOfBirth.Year;
        return asOf < dateOfBirth.AddYears(age) ? age - 1 : age;
    }

    public void UpdateProfile(
        PersonName name,
        ContactInfo contact,
        string jobTitle,
        Guid tradeId,
        DateOnly? dateOfBirth,
        Address? address)
    {
        // Checked against the hire date rather than "today": nobody is hired before they
        // are born, so this catches future and nonsense dates without the domain ever
        // reading the system clock. Clock-free rules are the ones you can actually test.
        if (dateOfBirth is { } dob && dob >= HireDate)
        {
            throw new DomainException("Date of birth must be before the hire date.");
        }

        Name = name;
        Contact = contact;
        JobTitle = Guard.Required(jobTitle, "Job title", 120);
        TradeId = Guard.Required(tradeId, "Trade");
        DateOfBirth = dateOfBirth;
        Address = address;
    }

    public void AssignManager(Guid? managerId)
    {
        if (managerId == Id)
        {
            throw new DomainException("An employee cannot report to themselves.");
        }

        ManagerId = managerId;
    }

    /// <summary>Links a login to this person. One-way: accounts are never silently reassigned.</summary>
    public void LinkToUser(Guid userId)
    {
        if (UserId is not null)
        {
            throw new DomainException("This employee is already linked to a user account.");
        }

        UserId = Guard.Required(userId, "User");
    }

    #endregion

    #region Employment status

    public void Suspend()
    {
        if (Status is not EmploymentStatus.Active)
        {
            throw new DomainException($"Only an active employee can be suspended (current status: {Status}).");
        }

        Status = EmploymentStatus.Suspended;
    }

    public void Reinstate()
    {
        if (Status is not EmploymentStatus.Suspended)
        {
            throw new DomainException($"Only a suspended employee can be reinstated (current status: {Status}).");
        }

        Status = EmploymentStatus.Active;
    }

    public void Terminate()
    {
        if (Status is EmploymentStatus.Terminated)
        {
            throw new DomainException("This employee has already been terminated.");
        }

        Status = EmploymentStatus.Terminated;
    }

    #endregion

    #region Certifications

    public IReadOnlyList<Certification> Certifications => _certifications;

    /// <summary>
    /// Records a qualification. Renewals are added as new rows rather than overwriting the
    /// old one, so the history of who was qualified when survives an audit.
    /// </summary>
    public Certification AddCertification(
        Guid certificationTypeId,
        string issuedBy,
        DateOnly issuedOn,
        DateOnly expiresOn,
        string? referenceNumber = null)
    {
        var certification = new Certification(Id, certificationTypeId, issuedBy, issuedOn, expiresOn, referenceNumber);
        _certifications.Add(certification);
        return certification;
    }

    public void RemoveCertification(Guid certificationId)
    {
        var certification = _certifications.SingleOrDefault(c => c.Id == certificationId)
                            ?? throw new DomainException("This employee has no such certification.");

        _certifications.Remove(certification);
    }

    /// <summary>
    /// The question the permit module will actually ask: on this date, was this person
    /// qualified for this kind of work? Answered from the dates, so it cannot go stale.
    /// </summary>
    public bool HasValidCertification(Guid certificationTypeId, DateOnly asOf) =>
        _certifications.Any(c => c.CertificationTypeId == certificationTypeId && c.IsValidOn(asOf));

    #endregion
}
