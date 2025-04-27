using System.Diagnostics;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using SafeObject.Core.Models;
using SafeObject.Core.Services;

namespace UsageExample;

internal static class Program
{
    private static async Task Main(string[] args)
    {
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Debug);
        });
        var logger = loggerFactory.CreateLogger<StorageService>();

        using var vaultService = new VaultService();
        using var objectStorage = new StorageService(logger, vaultService);

        try
        {
            var sourcePath = args.Length > 0
                ? args[0]
                : GetInputFilePath();

            var outputDirectory = Path.GetDirectoryName(sourcePath) ?? string.Empty;
            var encryptedFileName = $"encrypted_{Path.GetFileName(sourcePath)}";
            var decryptedFileName = $"decrypted_{Path.GetFileName(sourcePath)}";

            var destinationPath = Path.Combine(outputDirectory, encryptedFileName);
            var decryptedPath = Path.Combine(outputDirectory, decryptedFileName);

            var filePublicMasterKey = GenerateAesKey();
            var fileId = Guid.NewGuid().ToString();

            var stopwatch = Stopwatch.StartNew();

            Console.WriteLine("Starting encryption process...");
            var encryptRequest = new FileProcessingRequest(fileId, sourcePath, destinationPath);

            await objectStorage.EncryptFileAsync(encryptRequest, filePublicMasterKey, CancellationToken.None);
            Console.WriteLine("Encryption completed successfully");

            Console.WriteLine("\nStarting decryption process...");
            var decryptRequest = new FileProcessingRequest(fileId, destinationPath, decryptedPath);
            await objectStorage.DecryptFileAsync(decryptRequest, filePublicMasterKey, CancellationToken.None);
            Console.WriteLine("Decryption completed successfully");

            stopwatch.Stop();

            DisplayResults(sourcePath, destinationPath, decryptedPath, stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            Console.ResetColor();
        }
    }

    private static string GetInputFilePath()
    {
        Console.WriteLine("Please enter the path to the file you want to encrypt/decrypt:");
        var path = Console.ReadLine();

        while (string.IsNullOrWhiteSpace(path) || File.Exists(path) is not true)
        {
            Console.WriteLine("Invalid file path. Please enter a valid path to an existing file:");
            path = Console.ReadLine();
        }

        return path;
    }

    private static void DisplayResults(string sourcePath, string destinationPath, string decryptedPath,
        TimeSpan elapsed)
    {
        Console.WriteLine("\n=== OPERATION SUMMARY ===");
        Console.WriteLine(
            $"Total time elapsed: {elapsed.TotalSeconds:F2} seconds ({elapsed.TotalMinutes:F2} minutes)");

        Console.WriteLine("\n=== FILE INFORMATION ===");
        DisplayFileInfo("Original", sourcePath);
        DisplayFileInfo("Encrypted", destinationPath);
        DisplayFileInfo("Decrypted", decryptedPath);

        Console.WriteLine("\nPress any key to exit...");
        Console.ReadKey();
    }

    private static void DisplayFileInfo(string fileType, string path)
    {
        var fileInfo = new FileInfo(path);
        Console.WriteLine($"{fileType} file: {path}");
        Console.WriteLine($"  - Size: {FormatFileSize(fileInfo.Length)}");
    }

    private static string FormatFileSize(long bytes)
    {
        string[] suffixes = ["B", "KB", "MB", "GB", "TB"];
        var counter = 0;
        decimal number = bytes;

        while (Math.Round(number / 1024) >= 1)
        {
            number /= 1024;
            counter++;
        }

        return $"{number:n2} {suffixes[counter]}";
    }

    private static string GenerateAesKey(int keySize = 256)
    {
        if (keySize is not (128 or 192 or 256))
            throw new ArgumentOutOfRangeException(nameof(keySize), "Key size must be 128, 192, or 256 bits.");

        var initialKey = new byte[keySize / 8];
        RandomNumberGenerator.Fill(initialKey);

        using var keyDerivation = new Rfc2898DeriveBytes(
            initialKey,
            RandomNumberGenerator.GetBytes(32),
            100000,
            HashAlgorithmName.SHA256);

        var finalKey = keyDerivation.GetBytes(keySize / 8);
        return Convert.ToBase64String(finalKey);
    }
}