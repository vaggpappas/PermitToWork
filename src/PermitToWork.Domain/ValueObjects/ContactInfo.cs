using System.Net.Mail;
using PermitToWork.Domain.Common;

namespace PermitToWork.Domain.ValueObjects;

/// <summary>
/// How to reach a person. Email is required — it is the account identity and the channel
/// permit notifications go to; a phone number is not always known.
/// </summary>
public sealed record ContactInfo
{
    private const int MaxEmailLength = 254;   // RFC 5321
    private const int MaxPhoneLength = 30;

    public string Email { get; }
    public string? PhoneNumber { get; }

    private ContactInfo(string email, string? phoneNumber)
    {
        Email = email;
        PhoneNumber = phoneNumber;
    }

    public static ContactInfo Create(string? email, string? phoneNumber)
    {
        var candidate = Guard.Required(email, "Email", MaxEmailLength).ToLowerInvariant();

        // MailAddress rather than a hand-rolled regex: email grammar is genuinely hard and
        // every regex found online gets some real address wrong.
        //
        // The second half of this check is not optional. MailAddress also parses the
        // "Display Name <someone@example.com>" form, so "two spaces@here.local" succeeds
        // with DisplayName "two" and Address "spaces@here.local" — and we would then store
        // the whole original string as the email. Requiring the parsed address to equal
        // the input is what rejects anything with a display name, stray spaces included.
        if (!MailAddress.TryCreate(candidate, out var parsed) || parsed.Address != candidate)
        {
            throw new DomainException($"'{candidate}' is not a valid email address.");
        }

        return new ContactInfo(candidate, Guard.Optional(phoneNumber, "Phone number", MaxPhoneLength));
    }

    public override string ToString() => Email;
}
