namespace safe_object.Models;

public sealed record FileProcessingRequest(string FileId, string SourcePath, string DestinationPath);