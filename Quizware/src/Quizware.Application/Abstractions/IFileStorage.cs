namespace Quizware.Application.Abstractions;

/// <summary>Where uploaded bytes actually land — outside the web root
/// (NFR-S6). Local disk today; swappable for blob storage later without
/// touching any handler.</summary>
public interface IFileStorage
{
    /// <summary>Saves the content under a name derived from
    /// <paramref name="fileExtension"/> (never the caller-supplied original
    /// file name — avoids path traversal) and returns the stored path/key
    /// to persist on the MediaAsset.</summary>
    Task<string> SaveAsync(byte[] content, string fileExtension, CancellationToken cancellationToken);
}
