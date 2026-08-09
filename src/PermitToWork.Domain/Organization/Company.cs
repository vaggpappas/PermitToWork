using PermitToWork.Domain.Common;

namespace PermitToWork.Domain.Organization;

/// <summary>
/// Whether a company owns the site or works on it. This is not cosmetic: it decides
/// whether a user's data access is site-wide or scoped to their own company, so it is an
/// enum rather than an <c>IsContractor</c> flag — a boolean loses its meaning the moment
/// it is passed to a method as <c>true</c>.
/// </summary>
public enum CompanyKind
{
    Owner = 1,
    Contractor = 2
}

/// <summary>
/// An employer: either the plant operator or a contractor firm working on site.
/// Separate from the physical hierarchy — a contractor works across many facilities and a
/// facility hosts many contractors, so "who employs you" and "where you are" are
/// independent questions.
/// </summary>
public class Company : Entity
{
    private Company() { }

    public Company(string code, string name, CompanyKind kind)
    {
        Code = Guard.Required(code, "Company code", 20).ToUpperInvariant();
        Name = Guard.Required(name, "Company name", 200);
        Kind = kind;
    }

    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public CompanyKind Kind { get; private set; }

    /// <summary>
    /// Reference data uses a flag rather than a status enum: there is no third state, and
    /// unlike a bare <c>bool</c> parameter the property name travels with the value.
    /// </summary>
    public bool IsActive { get; private set; } = true;

    public void Rename(string name) => Name = Guard.Required(name, "Company name", 200);

    public void Deactivate() => IsActive = false;

    public void Reactivate() => IsActive = true;
}
