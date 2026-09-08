using Quizware.Domain.Common;
using Quizware.Domain.Enums;

namespace Quizware.Domain.QuestionBank;

/// <summary>Replaces plain path strings. Files are validated (magic-byte
/// check), deduplicated (checksum) and tracked.</summary>
public sealed class MediaAsset : BaseEntity, IAuditable, ISoftDeletable
{
    private MediaAsset()
    {
        FileName = string.Empty;
        StoredPath = string.Empty;
        MimeType = string.Empty;
        ChecksumSha256 = Array.Empty<byte>();
        CreatedBy = string.Empty;
    }

    public static MediaAsset Create(
        string fileName, string storedPath, MediaKind mediaType, string mimeType,
        long fileSizeBytes, byte[] checksumSha256, string createdBy, Guid? programId = null)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException("FileName is required.", nameof(fileName));
        }

        if (string.IsNullOrWhiteSpace(storedPath))
        {
            throw new ArgumentException("StoredPath is required.", nameof(storedPath));
        }

        if (checksumSha256.Length != 32)
        {
            throw new ArgumentException("A SHA-256 checksum is 32 bytes.", nameof(checksumSha256));
        }

        return new MediaAsset
        {
            ProgramId = programId,
            FileName = fileName,
            StoredPath = storedPath,
            MediaType = mediaType,
            MimeType = mimeType,
            FileSizeBytes = fileSizeBytes,
            ChecksumSha256 = checksumSha256,
            IsValidated = false,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = createdBy,
        };
    }

    public Guid? ProgramId { get; private set; }
    public string FileName { get; private set; }
    public string StoredPath { get; private set; }
    public MediaKind MediaType { get; private set; }
    public string MimeType { get; private set; }
    public long FileSizeBytes { get; private set; }
    public int? DurationSeconds { get; private set; }
    public int? Width { get; private set; }
    public int? Height { get; private set; }
    public byte[] ChecksumSha256 { get; private set; }
    public bool IsValidated { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }
    public string CreatedBy { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }
    public string? UpdatedBy { get; private set; }

    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAtUtc { get; private set; }

    /// <summary>Marks that the file passed the magic-byte check.</summary>
    public void MarkValidated()
    {
        IsValidated = true;
    }
}
