using PermitToWork.Domain.Common;

namespace PermitToWork.Domain.Organization;

// Admin-managed reference data. Kept as tables rather than enums because the site adds
// trades and certification types without a redeploy, and permits must be able to point at
// a specific one by id.
//
// Trade and CertificationType share three properties. They do NOT share a base class:
// three repeated lines are cheaper than an inheritance hierarchy that EF Core would have
// to map, and the two concepts will diverge (certification types will grow a validity
// period, trades will not).

/// <summary>
/// The craft an employee practises — "Welder Gr.3", "Pipe Fitter", "Electrician".
/// Rule-bearing: a hot work permit requires a certified welder, so this is a reference to
/// a known trade rather than free text that would have to be parsed to make a safety call.
/// </summary>
public class Trade : Entity
{
    private Trade() { }

    public Trade(string code, string name)
    {
        Code = Guard.Required(code, "Trade code", 20).ToUpperInvariant();
        Name = Guard.Required(name, "Trade name", 100);
    }

    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public bool IsActive { get; private set; } = true;

    public void Rename(string name) => Name = Guard.Required(name, "Trade name", 100);

    public void Deactivate() => IsActive = false;
}

/// <summary>
/// A kind of qualification — "Hot Work", "Confined Space Entry", "Working at Height",
/// "LOTO", "First Aid".
/// </summary>
public class CertificationType : Entity
{
    private CertificationType() { }

    public CertificationType(string code, string name)
    {
        Code = Guard.Required(code, "Certification type code", 20).ToUpperInvariant();
        Name = Guard.Required(name, "Certification type name", 100);
    }

    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public bool IsActive { get; private set; } = true;

    public void Rename(string name) => Name = Guard.Required(name, "Certification type name", 100);

    public void Deactivate() => IsActive = false;
}
