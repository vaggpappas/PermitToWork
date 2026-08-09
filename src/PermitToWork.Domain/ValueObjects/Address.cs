using PermitToWork.Domain.Common;

namespace PermitToWork.Domain.ValueObjects;

/// <summary>
/// A postal address. Optional on an employee — but if one is given it must be complete,
/// because a half-filled address is worse than none: it looks like data and isn't.
/// </summary>
public sealed record Address
{
    public string Street { get; }
    public string City { get; }
    public string PostalCode { get; }
    public string Country { get; }

    private Address(string street, string city, string postalCode, string country)
    {
        Street = street;
        City = city;
        PostalCode = postalCode;
        Country = country;
    }

    public static Address Create(string? street, string? city, string? postalCode, string? country) => new(
        Guard.Required(street, "Street", 200),
        Guard.Required(city, "City", 100),
        Guard.Required(postalCode, "Postal code", 20),
        Guard.Required(country, "Country", 100));

    public override string ToString() => $"{Street}, {PostalCode} {City}, {Country}";
}
