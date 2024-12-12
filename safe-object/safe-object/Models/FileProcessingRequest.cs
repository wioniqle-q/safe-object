namespace safe_object.Models;

public sealed class FileProcessingRequest
{
    public string FileId { get; set; } = string.Empty;
    public string SourcePath { get; set; } = string.Empty;
    public string DestinationPath { get; set; } = string.Empty;
}