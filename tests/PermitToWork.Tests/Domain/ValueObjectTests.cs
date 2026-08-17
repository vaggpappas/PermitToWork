using FluentAssertions;
using PermitToWork.Domain.Common;
using PermitToWork.Domain.ValueObjects;
using Xunit;

// ReSharper disable once CheckNamespace

namespace PermitToWork.Tests.Domain;

/// <summary>
/// The value objects exist so that a malformed employee number or email cannot be
/// constructed at all. These tests are the proof of that claim.
/// </summary>
public class ValueObjectTests
{
    #region EmployeeNumber

    [Fact]
    public void EmployeeNumber_NormalisesToUpperCase()
    {
        EmployeeNumber.Create(" acme-0042 ").Value.Should().Be("ACME-0042");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("AB")]                       // shorter than three characters
    [InlineData("EMP 0042")]                 // space
    [InlineData("EMP_0042")]                 // underscore
    [InlineData("EMP#0042")]
    [InlineData("EMPLOYEE-NUMBER-THAT-IS-FAR-TOO-LONG")]
    public void EmployeeNumber_Rejects_MalformedInput(string? input)
    {
        var create = () => EmployeeNumber.Create(input);

        create.Should().Throw<DomainException>();
    }

    [Theory]
    [InlineData("EMP-00042")]
    [InlineData("ACME991")]
    [InlineData("W12345")]
    public void EmployeeNumber_Accepts_RealWorldSchemes(string input)
    {
        // Contractors bring their own numbering. A stricter pattern would reject real data.
        EmployeeNumber.Create(input).Value.Should().Be(input);
    }

    [Fact]
    public void EmployeeNumber_TryCreate_ReturnsNull_When_Malformed()
    {
        // The search box asks a question rather than asserting a fact, so a stray keystroke
        // must not become an exception.
        EmployeeNumber.TryCreate("not a number!").Should().BeNull();
    }

    [Fact]
    public void EmployeeNumber_TryCreate_NormalisesLikeCreate()
    {
        EmployeeNumber.TryCreate("acme-0042").Should().Be(EmployeeNumber.Create("ACME-0042"));
    }

    [Fact]
    public void EmployeeNumber_ComparesByValue()
    {
        // Two badges with the same number are the same badge. Reference equality here
        // would break every lookup that goes through the database and back.
        EmployeeNumber.Create("EMP-00042").Should().Be(EmployeeNumber.Create("EMP-00042"));
        EmployeeNumber.Create("EMP-00042").Should().NotBe(EmployeeNumber.Create("EMP-00043"));
    }

    #endregion

    #region PersonName

    [Fact]
    public void PersonName_ComposesFullName()
    {
        PersonName.Create("Nadia", "Kowalski").Full.Should().Be("Nadia Kowalski");
    }

    [Fact]
    public void PersonName_TrimsWhitespace()
    {
        PersonName.Create("  Nadia  ", " Kowalski ").Full.Should().Be("Nadia Kowalski");
    }

    [Theory]
    [InlineData(null, "Kowalski")]
    [InlineData("", "Kowalski")]
    [InlineData("Nadia", null)]
    [InlineData("Nadia", "  ")]
    public void PersonName_Rejects_MissingParts(string? first, string? last)
    {
        var create = () => PersonName.Create(first, last);

        create.Should().Throw<DomainException>();
    }

    #endregion

    #region ContactInfo

    [Fact]
    public void ContactInfo_LowercasesEmail()
    {
        ContactInfo.Create("Nadia.Kowalski@ACME.example", null).Email
            .Should().Be("nadia.kowalski@acme.example");
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("missing@")]
    [InlineData("@missing.local")]
    [InlineData("two spaces@here.local")]
    [InlineData("Nadia Kowalski <nadia@acme.example>")]
    public void ContactInfo_Rejects_MalformedEmail(string input)
    {
        var create = () => ContactInfo.Create(input, null);

        // The last two cases are the ones that matter. MailAddress happily parses a
        // display name, so on its own it accepts both of these — and ContactInfo would
        // then store the entire string as the address. This test caught that.
        create.Should().Throw<DomainException>();
    }

    [Fact]
    public void ContactInfo_TreatsBlankPhoneAsAbsent()
    {
        // "  " is not a phone number. Collapsing it to null means no query downstream has
        // to check for both null and whitespace.
        ContactInfo.Create("nadia@acme.example", "   ").PhoneNumber.Should().BeNull();
    }

    #endregion

    #region DateTimeRange

    [Fact]
    public void DateTimeRange_RejectsAnEndBeforeItsStart()
    {
        var backwards = () => DateTimeRange.Create(Given.WorkEnd, Given.WorkStart);

        backwards.Should().Throw<DomainException>().WithMessage("*after its start*");
    }

    [Fact]
    public void DateTimeRange_RejectsAZeroLengthPeriod()
    {
        var instant = () => DateTimeRange.Create(Given.WorkStart, Given.WorkStart);

        instant.Should().Throw<DomainException>();
    }

    [Fact]
    public void DateTimeRange_IsHalfOpen()
    {
        var window = Given.TheWorkWindow;

        window.Contains(Given.WorkStart).Should().BeTrue();
        window.Contains(Given.WorkEnd.AddSeconds(-1)).Should().BeTrue();

        // The end instant is the first moment outside, stated once so no caller has to
        // decide for themselves.
        window.Contains(Given.WorkEnd).Should().BeFalse();
        window.HasPassed(Given.WorkEnd).Should().BeTrue();
    }

    #endregion

    #region PermitNumber

    [Theory]
    [InlineData("HW-2026-0001")]
    [InlineData("CS-2026-0042")]
    [InlineData("LOTO-2026-0001")]
    public void PermitNumber_AcceptsTypeYearSequence(string input)
    {
        PermitNumber.Create(input).Value.Should().Be(input);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("HW-2026")]
    [InlineData("H-2026-0001")]
    [InlineData("HW-26-0001")]
    [InlineData("HW-2026-1")]
    [InlineData("HOTWORK-2026-0001")]
    public void PermitNumber_Rejects_MalformedInput(string? input)
    {
        var create = () => PermitNumber.Create(input);

        create.Should().Throw<DomainException>();
    }

    [Fact]
    public void PermitNumber_NormalisesToUpperCase()
    {
        PermitNumber.Create(" hw-2026-0001 ").Value.Should().Be("HW-2026-0001");
    }

    #endregion

    #region Address

    [Fact]
    public void Address_ComposesAllFourParts()
    {
        var address = Address.Create("12 Pireos Street", "Athens", "10553", "Greece");

        address.ToString().Should().Be("12 Pireos Street, 10553 Athens, Greece");
    }

    [Theory]
    [InlineData(null, "Athens", "10553", "Greece")]
    [InlineData("12 Pireos Street", null, "10553", "Greece")]
    [InlineData("12 Pireos Street", "Athens", null, "Greece")]
    [InlineData("12 Pireos Street", "Athens", "10553", null)]
    public void Address_Rejects_PartialAddress(string? street, string? city, string? postalCode, string? country)
    {
        var create = () => Address.Create(street, city, postalCode, country);

        // An address is optional, but a half-filled one is worse than none — it looks like
        // data and is not. Either all four or the whole thing is null.
        create.Should().Throw<DomainException>();
    }

    #endregion
}
