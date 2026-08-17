using PermitToWork.Domain.Organization;
using PermitToWork.Domain.Permits;
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

    #region Permits

    public static readonly Guid HotWorkPermitTypeId = Guid.Parse("77777777-7777-7777-7777-777777777777");
    public static readonly Guid MaintenanceCategoryId = Guid.Parse("88888888-8888-8888-8888-888888888888");
    public static readonly Guid LocationId = Guid.Parse("99999999-9999-9999-9999-999999999999");

    /// <summary>A three-day job, first week of September. Fixed, so nothing here drifts with the clock.</summary>
    public static readonly DateTimeOffset WorkStart = new(2026, 9, 1, 8, 0, 0, TimeSpan.Zero);

    public static readonly DateTimeOffset WorkEnd = new(2026, 9, 3, 17, 0, 0, TimeSpan.Zero);

    public static DateTimeRange TheWorkWindow => DateTimeRange.Create(WorkStart, WorkEnd);

    public static readonly CertificationRequirement HotWorkRequired =
        new(HotWorkCertificationTypeId, "Hot Work");

    public static Permit APermit(
        Guid createdBy,
        Guid receiver,
        IEnumerable<CertificationRequirement>? requires = null,
        DateTimeRange? validity = null) =>
        new(
            PermitNumber.Create("HW-2026-0001"),
            HotWorkPermitTypeId,
            MaintenanceCategoryId,
            "Replace the flange on the north header.",
            FacilityId,
            LocationId,
            validity ?? TheWorkWindow,
            createdBy,
            receiver,
            requires ?? [],
            project: "Unit 3 Turnaround");

    /// <summary>A permit that demands a Hot Work certificate of everyone on it.</summary>
    public static Permit AHotWorkPermit(Guid createdBy, Guid receiver, DateTimeRange? validity = null) =>
        APermit(createdBy, receiver, [HotWorkRequired], validity);

    /// <summary>
    /// A welder holding a Hot Work certificate. Pass an earlier expiry to build the case
    /// that matters: somebody qualified on the first morning but not on the last.
    /// </summary>
    public static Employee ACertifiedWelder(DateOnly? expiresOn = null, string number = "ACME-0100")
    {
        var welder = AnEmployee(number, "Luis", "Ferreira");

        welder.AddCertification(
            HotWorkCertificationTypeId,
            "Hellenic Welding Institute",
            new DateOnly(2025, 1, 15),
            expiresOn ?? new DateOnly(2027, 1, 15));

        return welder;
    }

    public static Employee AnUncertifiedWorker(string number = "ACME-0200") =>
        AnEmployee(number, "Marta", "Silva");

    public static ApproverAssignment Approver(Guid employeeId) => new(employeeId, IsDecisive: false);

    public static ApproverAssignment DecisiveApprover(Guid employeeId) => new(employeeId, IsDecisive: true);

    #endregion
}
