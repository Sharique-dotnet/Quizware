using System.Security.Cryptography;
using MediatR;
using Microsoft.EntityFrameworkCore;
using QuizApp.Application.Abstractions;
using QuizApp.Application.Authorization;
using QuizApp.Application.Common.Exceptions;
using QuizApp.Application.Media.Dtos;
using QuizApp.Domain.QuestionBank;

namespace QuizApp.Application.Media.Commands;

public sealed record UploadMediaCommand(Guid ProgramId, string FileName, long FileSizeBytes, byte[] Content, bool Shared)
    : IRequest<MediaAssetDto>;

/// <summary>P6-15: extension allow-list, magic-byte check, size cap,
/// SHA-256 dedup, storage outside the web root (via IFileStorage). A
/// renamed .exe is rejected because its bytes never match any allowed
/// signature — not because of its extension alone.</summary>
public sealed class UploadMediaCommandHandler : IRequestHandler<UploadMediaCommand, MediaAssetDto>
{
    private readonly IAppDbContext _db;
    private readonly IFileStorage _fileStorage;
    private readonly ICurrentUser _currentUser;

    public UploadMediaCommandHandler(IAppDbContext db, IFileStorage fileStorage, ICurrentUser currentUser)
    {
        _db = db;
        _fileStorage = fileStorage;
        _currentUser = currentUser;
    }

    public async Task<MediaAssetDto> Handle(UploadMediaCommand request, CancellationToken cancellationToken)
    {
        if (request.Shared && !_currentUser.IsInRole(Roles.SuperAdmin))
        {
            throw new UnauthorizedAccessException("Only SuperAdmin may upload a shared media asset.");
        }

        if (request.FileSizeBytes > MediaValidation.MaxFileSizeBytes)
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["file"] = [$"File exceeds the {MediaValidation.MaxFileSizeBytes / (1024 * 1024)} MB limit."],
            });
        }

        if (!MediaValidation.TryGetMediaKind(request.FileName, out var mediaKind, out var extension))
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["file"] = [$"'{extension}' is not an allowed file type."],
            });
        }

        if (!MediaValidation.HasValidMagicBytes(request.Content, extension))
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["file"] = ["The file's content does not match its extension."],
            });
        }

        var checksum = SHA256.HashData(request.Content);
        var ownerProgramId = request.Shared ? (Guid?)null : request.ProgramId;

        var existing = await _db.MediaAssets
            .SingleOrDefaultAsync(
                m => m.ChecksumSha256 == checksum && (m.ProgramId == ownerProgramId || m.ProgramId == null), cancellationToken);
        if (existing is not null)
        {
            return ToDto(existing, wasDeduplicated: true);
        }

        var actor = _currentUser.Email ?? "unknown";
        var storedPath = await _fileStorage.SaveAsync(request.Content, extension, cancellationToken);
        var mimeType = MimeTypeFor(extension);

        var mediaAsset = MediaAsset.Create(
            request.FileName, storedPath, mediaKind, mimeType, request.FileSizeBytes, checksum, actor, ownerProgramId);
        mediaAsset.MarkValidated();

        _db.MediaAssets.Add(mediaAsset);
        await _db.SaveChangesAsync(cancellationToken);

        return ToDto(mediaAsset, wasDeduplicated: false);
    }

    private static MediaAssetDto ToDto(MediaAsset asset, bool wasDeduplicated) => new(
        asset.Id, asset.FileName, asset.MediaType.ToString(), asset.MimeType, asset.FileSizeBytes, asset.IsValidated, asset.ProgramId, wasDeduplicated);

    private static string MimeTypeFor(string extension) => extension.ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".gif" => "image/gif",
        ".mp3" => "audio/mpeg",
        ".wav" => "audio/wav",
        ".mp4" => "video/mp4",
        ".webm" => "video/webm",
        _ => "application/octet-stream",
    };
}
