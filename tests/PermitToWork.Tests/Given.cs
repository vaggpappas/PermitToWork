using PermitToWork.Domain.Organization;
using PermitToWork.Domain.ValueObjects;

namespace PermitToWork.Tests;

/// <summary>
/// Test data. One place to build a valid employee or team, so that a test says what makes
/// it different rather than restating six constructor arguments that never vary.
/// <para>
/// Fixed dates throughout. A test that reads <c>DateTime.Now</c> passes for months and
/// then fails on somebody's birthday.
/// </para>
/// </summary>
internal static class Given
{
    public static readonly Guid AcmeCompanyId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid OwnerCompanyId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid WelderTradeId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    public static readonly Guid FacilityId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    public static readonly Guid HotWorkCertificationTypeId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    public static readonly Guid ConfinedSpaceCertificationTypeId = Guid.Parse("66666666-6666-6666-6666-666666666666");

    public static readonly DateOnly HireDate = new(2020, 1, 6);

    /// <summary>An arbitrary "now" for tests that need one. Never the system clock.</summary>
    public static readonly DateOnly Today = new(2026, 8, 14);

    public static Employee AnEmployee(
        string number = "EMP-00042",
        string first = "Nadia",
        string last = "Kowalski",
        DateOnly? hiredOn = null) =>
        new(
            EmployeeNumber.Create(number),
            PersonName.Create(first, last),
            ContactInfo.Create($"{first}.{last}@acme.example".ToLowerInvariant(), null),
            AcmeCompanyId,
            WelderTradeId,
            "Welder",
            hiredOn ?? HireDate);

    public static Team ATeam(string code = "MECH-A") =>
        new(code, "Mechanical Crew A", FacilityId, "Unit 3 mechanical maintenance");
}
