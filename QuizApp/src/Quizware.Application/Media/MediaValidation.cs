using Quizware.Domain.Enums;

namespace Quizware.Application.Media;

/// <summary>P6-15: extension allow-list + magic-byte check. Numbers (max
/// size, allowed extensions) aren't documented anywhere — none of the docs
/// give a concrete list or a byte cap — so these are this implementation's
/// own, deliberately conservative, decision.</summary>
public static class MediaValidation
{
    public const long MaxFileSizeBytes = 25 * 1024 * 1024;

    private static readonly IReadOnlyDictionary<string, MediaKind> AllowedExtensions = new Dictionary<string, MediaKind>(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = MediaKind.Image,
        [".jpeg"] = MediaKind.Image,
        [".png"] = MediaKind.Image,
        [".gif"] = MediaKind.Image,
        [".mp3"] = MediaKind.Audio,
        [".wav"] = MediaKind.Audio,
        [".mp4"] = MediaKind.Video,
        [".webm"] = MediaKind.Video,
    };

    public static bool TryGetMediaKind(string fileName, out MediaKind mediaKind, out string extension)
    {
        extension = Path.GetExtension(fileName);
        return AllowedExtensions.TryGetValue(extension, out mediaKind);
    }

    /// <summary>Checks the file's actual leading bytes against the
    /// signature expected for its claimed extension — this, not the
    /// extension string, is what stops a renamed .exe (MZ header) from
    /// being accepted as a .jpg.</summary>
    public static bool HasValidMagicBytes(byte[] content, string extension) => extension.ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" => content.Length >= 3 && content[0] == 0xFF && content[1] == 0xD8 && content[2] == 0xFF,
        ".png" => content.Length >= 8 && content[0] == 0x89 && content[1] == 0x50 && content[2] == 0x4E && content[3] == 0x47,
        ".gif" => content.Length >= 6 && content[0] == 0x47 && content[1] == 0x49 && content[2] == 0x46,
        ".mp3" => content.Length >= 3 &&
            ((content[0] == 0x49 && content[1] == 0x44 && content[2] == 0x33) || (content[0] == 0xFF && (content[1] & 0xE0) == 0xE0)),
        ".wav" => content.Length >= 12 && content[0] == 0x52 && content[1] == 0x49 && content[2] == 0x46 && content[3] == 0x46,
        ".mp4" => content.Length >= 12 && content[4] == 0x66 && content[5] == 0x74 && content[6] == 0x79 && content[7] == 0x70,
        ".webm" => content.Length >= 4 && content[0] == 0x1A && content[1] == 0x45 && content[2] == 0xDF && content[3] == 0xA3,
        _ => false,
    };
}
