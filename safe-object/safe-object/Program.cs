using System.Diagnostics;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using safe_object.Models;
using safe_object.Services;

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

    var filePublicMasterKey = GenerateAesKey();
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
}
catch (Exception e)
{
    Console.WriteLine(e);
}

return;

static string GenerateAesKey(int keySize = 256)
{
    if (keySize is not (128 or 192 or 256))
        throw new ArgumentOutOfRangeException(nameof(keySize), "Key size must be 128, 192, or 256 bits.");

    var initialKey = new byte[keySize / 8];
    RandomNumberGenerator.Fill(initialKey);

    using var keyDerivation = new Rfc2898DeriveBytes(
        initialKey,
        RandomNumberGenerator.GetBytes(32),
        100000,
        HashAlgorithmName.SHA512);

    var finalKey = keyDerivation.GetBytes(keySize / 8);
    return Convert.ToBase64String(finalKey);
}