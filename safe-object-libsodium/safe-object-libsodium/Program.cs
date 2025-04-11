using System.Diagnostics;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using safe_object.Models;
using safe_object.Services;
using Sodium;

var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Debug);
});

var logger = loggerFactory.CreateLogger<StorageService>();
var vaultService = new VaultService();
var objectStorage = new StorageService(logger, vaultService);

try
{
    const string sourcePath = @""; // Provide the path to the file you want to encrypt/decrypt
    
    var destinationPath = Path.Combine(Path.GetDirectoryName(sourcePath)!, "encrypted_" + Path.GetFileName(sourcePath));
    var decryptedPath = Path.Combine(Path.GetDirectoryName(sourcePath)!, "decrypted_" + Path.GetFileName(sourcePath));
    
    var filePublicMasterKey = GenerateSodiumKey();
    var fileId = Guid.NewGuid().ToString();

    var stopwatch = Stopwatch.StartNew();

    Console.WriteLine("Starting encryption...");
    var encryptRequest = new FileProcessingRequest(fileId, sourcePath, destinationPath);
    await objectStorage.EncryptFileAsync(encryptRequest, filePublicMasterKey, CancellationToken.None);

    Console.WriteLine("Encryption completed!");

    Console.WriteLine("Starting decryption...");
    var decryptRequest = new FileProcessingRequest(fileId, destinationPath, decryptedPath);
    await objectStorage.DecryptFileAsync(decryptRequest, filePublicMasterKey, CancellationToken.None);

    objectStorage.Dispose();
    vaultService.Dispose();

    Console.WriteLine("Decryption completed!");

    stopwatch.Stop();

    Console.WriteLine($"Time elapsed: {stopwatch.Elapsed}");
    Console.WriteLine($"Time elapsed (minutes): {stopwatch.Elapsed.TotalMinutes}");

    Console.WriteLine($"Original file: {sourcePath}");
    Console.WriteLine($"Encrypted file: {destinationPath}");
    Console.WriteLine($"Decrypted file: {decryptedPath}");

    Console.ReadKey();
}
catch (Exception e)
{
    Console.WriteLine(e);
}

return;

static string GenerateSodiumKey()
{
    var sodiumKey = SecretBox.GenerateKey();
    return Convert.ToBase64String(sodiumKey);
}