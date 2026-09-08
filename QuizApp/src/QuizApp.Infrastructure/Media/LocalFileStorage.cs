using Microsoft.Extensions.Options;
using QuizApp.Application.Abstractions;

namespace QuizApp.Infrastructure.Media;

public sealed class LocalFileStorage : IFileStorage
{
    private readonly MediaStorageOptions _options;

    public LocalFileStorage(IOptions<MediaStorageOptions> options)
    {
        _options = options.Value;
    }

    public async Task<string> SaveAsync(byte[] content, string fileExtension, CancellationToken cancellationToken)
    {
        var rootPath = Path.GetFullPath(_options.RootPath);
        Directory.CreateDirectory(rootPath);

        // Never the caller-supplied file name — a fresh Guid avoids both
        // path traversal and collisions.
        var storedFileName = $"{Guid.NewGuid():N}{fileExtension}";
        var fullPath = Path.Combine(rootPath, storedFileName);

        await File.WriteAllBytesAsync(fullPath, content, cancellationToken);

        return Path.Combine(_options.RootPath, storedFileName);
    }
}
