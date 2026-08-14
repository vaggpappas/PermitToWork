namespace PermitToWork.Domain.Organization;

/// <summary>
/// What an employee may do in the application.
/// <para>
/// This is a field on the employee record, set by a supervisor or an administrator — not a
/// separate membership list somewhere else. That is deliberate: the moment "what is Maria
/// allowed to do" has two homes, they disagree, and the one you did not check is the one
/// that mattered. The bearer token's role claim is issued from this value, so this
/// property is the only thing that decides access.
/// </para>
/// </summary>
public enum AccessRole
{
    /// <summary>Read-only. The default for everyone until somebody decides otherwise.</summary>
    Employee = 1,

    /// <summary>May create and modify teams, and manage their membership.</summary>
    Responsible = 2,

    /// <summary>May manage employee records and assign access roles.</summary>
    Supervisor = 3,

    /// <summary>May record and revoke certifications. Sees every company.</summary>
    SafetyOfficer = 4,

    /// <summary>Everything.</summary>
    Administrator = 5
}
