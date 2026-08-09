namespace PermitToWork.Domain.Common;

/// <summary>
/// Thrown when an operation would leave the domain in an invalid state — an employee
/// number that isn't one, a permit activated before approval, a second leader on a team.
/// <para>
/// This is deliberately distinct from <see cref="ArgumentException"/>: an argument
/// exception says "the caller made a programming mistake", a domain exception says
/// "the business rules forbid this". The API layer maps it to 400/409, never 500.
/// </para>
/// </summary>
public sealed class DomainException(string message) : Exception(message);
