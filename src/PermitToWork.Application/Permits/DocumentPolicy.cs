using PermitToWork.Application.Common;

namespace PermitToWork.Application.Permits;

/// <summary>A file on its way in. The stream is owned by the caller and not disposed here.</summary>
public sealed record DocumentUpload(string FileName, string ContentType, long Length, Stream Content);

/// <summary>A file on its way out.</summary>
public sealed record DocumentDownload(Stream Content, string FileName, string ContentType);

/// <summary>
/// What the policy is, in a form the client can render.
/// <para>
/// Served from <c>GET /api/permits/document-policy</c> so the hint shown above the file
/// picker is the same rule the server enforces. Written into an Angular template instead,
/// it would be correct until the first time somebody changed the limit.
/// </para>
/// </summary>
public sealed record DocumentPolicyDto(
    long MaxBytes,
    int MaxMegabytes,
    IReadOnlyList<string> AllowedExtensions,
    string Accept,
    string Description);

/// <summary>
/// What may be attached to a permit.
/// <para>
/// Both the extension and the reported content type are checked. Neither alone is worth
/// much — a browser will happily report any content type, and an extension is just the end
/// of a string — but an attacker has to get both past to place something unexpected, and
/// an honest user gets a clear message either way.
/// </para>
/// </summary>
public static class DocumentPolicy
{
    public const int MaxMegabytes = 10;
    public const long MaxBytes = MaxMegabytes * 1024L * 1024L;

    /// <summary>Extension to the content type it must arrive as.</summary>
    private static readonly Dictionary<string, string[]> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = ["application/pdf"],
        [".doc"] = ["application/msword"],
        [".docx"] = ["application/vnd.openxmlformats-officedocument.wordprocessingml.document"],
        [".png"] = ["image/png"],
        [".jpg"] = ["image/jpeg"],
        [".jpeg"] = ["image/jpeg"],
        [".gif"] = ["image/gif"],
        [".webp"] = ["image/webp"],
    };

    public static DocumentPolicyDto Describe() => new(
        MaxBytes,
        MaxMegabytes,
        Allowed.Keys.OrderBy(extension => extension).ToList(),
        // The accept attribute for the file picker, so the operating system's dialogue
        // filters to the right files before anybody uploads the wrong thing.
        string.Join(",", Allowed.Keys.Concat(Allowed.Values.SelectMany(types => types)).Distinct()),
        $"PDF, Word or an image, up to {MaxMegabytes} MB. Documents are optional.");

    /// <summary>
    /// Throws unless the file is something a permit may carry. Runs before a single byte is
    /// written to disk — an oversized file that is rejected after being saved has already
    /// cost you the disk space.
    /// </summary>
    public static void EnsureAcceptable(string fileName, string contentType, long length)
    {
        if (length <= 0)
        {
            throw new ConflictException("That file is empty.");
        }

        if (length > MaxBytes)
        {
            var megabytes = length / 1024d / 1024d;
            throw new ConflictException(
                $"That file is {megabytes:F1} MB. The limit is {MaxMegabytes} MB.");
        }

        var extension = Path.GetExtension(fileName);

        if (string.IsNullOrEmpty(extension) || !Allowed.TryGetValue(extension, out var expectedTypes))
        {
            throw new ConflictException(
                $"'{extension}' files cannot be attached. Allowed: {string.Join(", ", Allowed.Keys)}.");
        }

        if (!expectedTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase))
        {
            throw new ConflictException(
                $"That file says it is {contentType}, which does not match a {extension} file.");
        }
    }
}
