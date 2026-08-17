namespace PermitToWork.Application.Abstractions;

/// <summary>
/// Somewhere to keep the bytes of an uploaded file.
/// <para>
/// The application never learns where that is. It hands over a stream and gets back an
/// opaque key, which it stores on the document row. Today the implementation writes to a
/// folder; if this ever moved to blob storage, the key would start meaning something else
/// and nothing above this interface would change.
/// </para>
/// </summary>
public interface IFileStorage
{
    /// <summary>
    /// Stores the content and returns the key needed to read it back.
    /// <para>
    /// The original file name is passed for its extension only. The key is generated — a
    /// name that came from a browser must never decide where a file lands on disk.
    /// </para>
    /// </summary>
    Task<string> SaveAsync(Stream content, string originalFileName, CancellationToken cancellationToken = default);

    /// <summary>Opens stored content, or null if the key no longer resolves to anything.</summary>
    Task<Stream?> OpenAsync(string storageKey, CancellationToken cancellationToken = default);

    Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default);
}
