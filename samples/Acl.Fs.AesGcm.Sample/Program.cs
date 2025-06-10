using System.Security.Cryptography;
using Acl.Fs.Core.Extensions;
using Acl.Fs.Core.Interfaces.Decryption.AesGcm;
using Acl.Fs.Core.Interfaces.Encryption.AesGcm;
using Acl.Fs.Core.Models;
using Acl.Fs.Core.Models.AesGcm;
using Acl.Fs.Vault.Abstractions.Interfaces;
using Acl.Fs.Vault.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Acl.Fs.AesGcm.Sample;

internal static class Program
{
    private static string GenerateAesKey()
    {
        using var aes = Aes.Create();
        aes.KeySize = 256;
        aes.GenerateKey();
        return Convert.ToBase64String(aes.Key);
    }

    private static byte[] GenerateSecureKey(int keySize = 32)
    {
        var keyBuffer = new byte[keySize];
        RandomNumberGenerator.Fill(keyBuffer);
        return keyBuffer;
    }

    private static async Task ProcessFileAsync(
        IServiceProvider serviceProvider,
        string sourceFilePath,
        string masterPublicKey,
        CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        
        var encryptionService = scope.ServiceProvider.GetRequiredService<IAesEncryptionService>();
        var decryptionService = scope.ServiceProvider.GetRequiredService<IAesDecryptionService>();
        var vaultService = scope.ServiceProvider.GetRequiredService<IVaultService>();
        
        try
        {
            var fileExtension = Path.GetExtension(sourceFilePath);
            var fileName = Path.GetFileNameWithoutExtension(sourceFilePath);
            var encryptedFilePath = Path.Combine(
                Path.GetDirectoryName(sourceFilePath)!,
                $"encrypted_{fileName}{fileExtension}");
            var decryptedFilePath = Path.Combine(
                Path.GetDirectoryName(sourceFilePath)!,
                $"decrypted_{fileName}{fileExtension}");

            var encryptInstruction = new FileTransferInstruction(
                Guid.NewGuid().ToString(),
                sourceFilePath,
                encryptedFilePath);
            
            Console.WriteLine($"Preparing to encrypt {Path.GetFileName(sourceFilePath)}...");
            var key = GenerateSecureKey();

            await vaultService.StoreEncryptionKeyAsync(
                encryptInstruction.FileId,
                Convert.ToBase64String(key),
                masterPublicKey);

            Console.WriteLine($"Storing encryption key for {Path.GetFileName(sourceFilePath)}...");
            Console.WriteLine($"Encryption key stored successfully.");

            var input = new AesEncryptionInput(key);
            
            Console.WriteLine($"Encrypting {Path.GetFileName(sourceFilePath)}...");
            await encryptionService.EncryptFileAsync(encryptInstruction, input, cancellationToken);
            Console.WriteLine($"File encrypted successfully to {encryptedFilePath}");

            var decryptInstruction = new FileTransferInstruction(
                encryptInstruction.FileId,
                encryptedFilePath,
                decryptedFilePath);

            Console.WriteLine($"Retrieving encryption key for {Path.GetFileName(sourceFilePath)}...");

            var retrievedKey = await vaultService.RetrieveEncryptionKeyAsync(
                encryptInstruction.FileId,
                masterPublicKey);

            var decryptionKey = Convert.FromBase64String(retrievedKey);

            Console.WriteLine($"Decryption key retrieved successfully.");

            var aesDecryptionInput = new AesDecryptionInput(decryptionKey);
            
            Console.WriteLine($"Decrypting {Path.GetFileName(encryptedFilePath)}...");
            await decryptionService.DecryptFileAsync(decryptInstruction, aesDecryptionInput, cancellationToken);
            Console.WriteLine($"File decrypted successfully to {decryptedFilePath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing {Path.GetFileName(sourceFilePath)}: {ex.Message}");
        }
    }

    private static async Task Main()
    {
        var serviceProvider = new ServiceCollection()
            .AddAclFsCore()
            .AddAclVault()
            .AddLogging(configure => configure.AddConsole())
            .BuildServiceProvider();

        var sourceFilePaths = new[]
        {
            Path.Combine(@"")
        };

        var masterPublicKey = GenerateAesKey();
        var cancellationToken = CancellationToken.None;

        try
        {
            var tasks = sourceFilePaths.Select(filePath =>
                Task.Run(() => ProcessFileAsync(serviceProvider, filePath, masterPublicKey, cancellationToken),
                    cancellationToken));

            await Task.WhenAll(tasks);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred during concurrent processing: {ex.Message}");
        }
        finally
        {
            await serviceProvider.DisposeAsync();
        }

        Console.WriteLine("Sample completed. Press any key to exit.");
        Console.ReadKey();
    }
}