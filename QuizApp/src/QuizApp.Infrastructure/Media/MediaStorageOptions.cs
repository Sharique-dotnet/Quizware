namespace QuizApp.Infrastructure.Media;

public sealed class MediaStorageOptions
{
    public const string SectionName = "MediaStorage";

    /// <summary>Deliberately outside wwwroot/the web root (NFR-S6) —
    /// defaults to an App_Data-style folder next to the app, never served
    /// as static content.</summary>
    public string RootPath { get; set; } = "App_Data/media";
}
