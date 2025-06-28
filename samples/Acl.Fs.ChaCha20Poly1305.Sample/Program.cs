using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Cryptography;
using Acl.Fs.Core.Extensions;
using Acl.Fs.Core.Interfaces.Decryption.ChaCha20Poly1305;
using Acl.Fs.Core.Interfaces.Encryption.ChaCha20Poly1305;
using Acl.Fs.Core.Models;
using Acl.Fs.Core.Models.ChaCha20Poly1305;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Acl.Fs.ChaCha20Poly1305.Sample;

public sealed class SampleVaultService(int maxConcurrency = 16) : IDisposable
{
    private readonly ConcurrentDictionary<string, byte[]> _keyStorage = new();
    private readonly SemaphoreSlim _semaphore = new(maxConcurrency, maxConcurrency);
    private bool _disposed;

    public void Dispose()
    {
        if (_disposed) return;

        _semaphore.Dispose();

        foreach (var key in _keyStorage.Values) Array.Clear(key, 0, key.Length);
        _keyStorage.Clear();

        _disposed = true;
    }

    public async Task StoreEncryptionKeyAsync(string fileId, ReadOnlyMemory<byte> key, string masterPublicKey)
    {
        ThrowIfDisposed();

        await _semaphore.WaitAsync();
        try
        {
            var keyArray = ArrayPool<byte>.Shared.Rent(key.Length);
            try
            {
                key.Span.CopyTo(keyArray);

                var storedKey = new byte[key.Length];
                Array.Copy(keyArray, storedKey, key.Length);

                _keyStorage[fileId] = storedKey;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(keyArray, true);
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<byte[]> RetrieveEncryptionKeyAsync(string fileId, string masterPublicKey)
    {
        ThrowIfDisposed();
        await _semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_keyStorage.TryGetValue(fileId, out var key) is not true)
                throw new KeyNotFoundException($"Key not found for file ID: {fileId}");

            var result = new byte[key.Length];
            Array.Copy(key, result, key.Length);

            return result;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(SampleVaultService));
    }
}

internal static class Program
{
    private static readonly int MaxConcurrency = Math.Max(1, Environment.ProcessorCount - 1);
    
    private static byte[] GenerateSecureChaCha20Key()
    {
        const int keySize = 32; 
        var keyBuffer = ArrayPool<byte>.Shared.Rent(keySize);
        try
        {
            RandomNumberGenerator.Fill(keyBuffer.AsSpan(0, keySize));

            var result = new byte[keySize];
            Array.Copy(keyBuffer, result, keySize);

            return result;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(keyBuffer, true);
        }
    }
    
    private static string GenerateChaCha20MasterKey()
    {
        var key = GenerateSecureChaCha20Key();
        try
        {
            return Convert.ToBase64String(key);
        }
        finally
        {
            Array.Clear(key, 0, key.Length);
        }
    }

    private static async Task<bool> ProcessFileAsync(
        IServiceProvider serviceProvider,
        string sourceFilePath,
        string masterPublicKey,
        SampleVaultService vaultService,
        SemaphoreSlim concurrencySemaphore,
        CancellationToken cancellationToken)
    {
        await concurrencySemaphore.WaitAsync(cancellationToken);

        try
        {
            using var scope = serviceProvider.CreateScope();

            var encryptionService = scope.ServiceProvider.GetRequiredService<IChaCha20Poly1305EncryptionService>();
            var decryptionService = scope.ServiceProvider.GetRequiredService<IChaCha20Poly1305DecryptionService>();

            if (File.Exists(sourceFilePath) is not true)
            {
                Console.WriteLine($"File not found: {sourceFilePath}");
                return false;
            }

            var fileInfo = new FileInfo(sourceFilePath);
            var fileName = Path.GetFileNameWithoutExtension(sourceFilePath);

            var fileExtension = fileInfo.Extension;
            var directory = fileInfo.DirectoryName!;

            var encryptedFilePath = Path.Combine(directory, $"chacha20_encrypted_{fileName}{fileExtension}");
            var decryptedFilePath = Path.Combine(directory, $"chacha20_decrypted_{fileName}{fileExtension}");

            var fileId = Guid.NewGuid().ToString();
            var encryptInstruction = new FileTransferInstruction(fileId, sourceFilePath, encryptedFilePath);

            Console.WriteLine(
                $"[{Thread.CurrentThread.ManagedThreadId}] Processing {fileInfo.Name} ({fileInfo.Length:N0} bytes) with ChaCha20Poly1305...");

            var key = GenerateSecureChaCha20Key();
            try
            {
                await vaultService.StoreEncryptionKeyAsync(fileId, key, masterPublicKey);

                var encryptionInput = new ChaCha20Poly1305EncryptionInput(key);

                var encryptionStopwatch = Stopwatch.StartNew();
                await encryptionService.EncryptFileAsync(encryptInstruction, encryptionInput, cancellationToken);
                encryptionStopwatch.Stop();

                Console.WriteLine(
                    $"[{Thread.CurrentThread.ManagedThreadId}] ChaCha20Poly1305 encrypted {fileInfo.Name} in {encryptionStopwatch.ElapsedMilliseconds}ms");

                var retrievedKey = await vaultService.RetrieveEncryptionKeyAsync(fileId, masterPublicKey);
                try
                {
                    var decryptInstruction = new FileTransferInstruction(fileId, encryptedFilePath, decryptedFilePath);
                    var decryptionInput = new ChaCha20Poly1305DecryptionInput(retrievedKey);

                    var decryptionStopwatch = Stopwatch.StartNew();
                    await decryptionService.DecryptFileAsync(decryptInstruction, decryptionInput, cancellationToken);
                    decryptionStopwatch.Stop();

                    Console.WriteLine(
                        $"[{Thread.CurrentThread.ManagedThreadId}] ChaCha20Poly1305 decrypted {fileInfo.Name} in {decryptionStopwatch.ElapsedMilliseconds}ms");
                    return true;
                }
                finally
                {
                    Array.Clear(retrievedKey, 0, retrievedKey.Length);
                }
            }
            finally
            {
                Array.Clear(key, 0, key.Length);
            }
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine($"Operation cancelled for {Path.GetFileName(sourceFilePath)}");
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing {Path.GetFileName(sourceFilePath)}: {ex.Message}");
            return false;
        }
        finally
        {
            concurrencySemaphore.Release();
        }
    }

    private static async Task Main()
    {
        var serviceProvider = new ServiceCollection()
            .AddAclFsCore()
            .AddChaCha20Poly1305Services()
            .AddLogging(configure => configure.AddConsole().SetMinimumLevel(LogLevel.Debug))
            .BuildServiceProvider();

        var sourceFilePaths = new[]
        {
            Path.Combine(@"", "")
        }.Where(File.Exists).ToArray();

        if (sourceFilePaths.Length is 0)
        {
            Console.WriteLine("No valid files found to process.");
            Console.WriteLine("Please update the sourceFilePaths array with valid file paths.");
            Console.ReadKey();
            return;
        }

        var masterPublicKey = GenerateChaCha20MasterKey();
        using var cts = new CancellationTokenSource();
        using var vaultService = new SampleVaultService(MaxConcurrency);
        using var semaphore = new SemaphoreSlim(MaxConcurrency, MaxConcurrency);

        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
            Console.WriteLine("\nCancellation requested...");
        };

        try
        {
            Console.WriteLine($"Processing {sourceFilePaths.Length} files with ChaCha20Poly1305 (max concurrency: {MaxConcurrency})");
            var overallStopwatch = Stopwatch.StartNew();

            var parallelOptions = new ParallelOptions
            {
                CancellationToken = cts.Token,
                MaxDegreeOfParallelism = MaxConcurrency
            };

            var results = new ConcurrentBag<bool>();

            await Parallel.ForEachAsync(
                sourceFilePaths,
                parallelOptions,
                async (filePath, ct) =>
                {
                    var success = await ProcessFileAsync(
                        serviceProvider,
                        filePath,
                        masterPublicKey,
                        vaultService,
                        semaphore,
                        ct);
                    results.Add(success);
                });

            overallStopwatch.Stop();

            var successCount = results.Count(r => r);
            var totalCount = results.Count;

            Console.WriteLine($"\nCompleted ChaCha20Poly1305 processing of {totalCount} files in {overallStopwatch.ElapsedMilliseconds}ms");
            Console.WriteLine($"Success: {successCount}/{totalCount}");

            if (successCount < totalCount) Console.WriteLine($"Failed: {totalCount - successCount}");
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Operation was cancelled.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred during processing: {ex.Message}");
        }
        finally
        {
            await serviceProvider.DisposeAsync();
        }

        Console.WriteLine("\nChaCha20Poly1305 processing completed. Press any key to exit.");
        Console.ReadKey();

        if (vaultService is IDisposable disposableVault) disposableVault.Dispose();
        Console.WriteLine("Vault service disposed.");

        GC.Collect(2, GCCollectionMode.Optimized, false);
        GC.WaitForPendingFinalizers();
        GC.Collect(2, GCCollectionMode.Optimized, false);

        Console.WriteLine("Garbage collection completed. Press any key to exit.");
        Console.ReadKey();
    }
}
