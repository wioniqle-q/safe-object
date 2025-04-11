using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using safe_object.Interfaces;
using safe_object.Models;
using static safe_object.Constants;

namespace safe_object.Services;

public sealed class VaultService : IVaultService, IDisposable
{
    private readonly ConcurrentDictionary<string, EncryptionKey> _keyStore = new();
    private readonly string _systemSecurityKey = GenerateSystemSecurityKey(256);
    private bool _disposed;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public async Task<string> StoreKeyAsync(string fileId, string filePrivateKey, string filePublicMasterKey)
    {
        ThrowIfDisposed();

        var encryptedPrivateKey = await EncryptAsync(filePrivateKey, filePublicMasterKey).ConfigureAwait(false);
        var finalEncryptedKey = await EncryptAsync(encryptedPrivateKey, _systemSecurityKey).ConfigureAwait(false);

        var encryptionKey = new EncryptionKey(fileId, finalEncryptedKey);

        if (_keyStore.TryAdd(fileId, encryptionKey) is not true)
            throw new InvalidOperationException($"Key for file ID {fileId} already exists.");

        return finalEncryptedKey;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public async Task<string> RetrieveKeyAsync(string fileId, string filePublicMasterKey)
    {
        ThrowIfDisposed();

        if (_keyStore.TryGetValue(fileId, out var encryptionKey) is not true)
            throw new KeyNotFoundException($"No key found for file ID: {fileId}");

        var decryptedLayerOne = await DecryptAsync(encryptionKey.EncryptedFilePrivateKey, _systemSecurityKey)
            .ConfigureAwait(false);
        return await DecryptAsync(decryptedLayerOne, filePublicMasterKey).ConfigureAwait(false);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            _keyStore.Clear();
            CleanMemory([Encoding.UTF8.GetBytes(_systemSecurityKey)]);
        }

        _disposed = true;
    }

    ~VaultService()
    {
        Dispose(false);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ThrowIfDisposed()
    {
        if (_disposed is not true) return;

        throw new ObjectDisposedException(nameof(VaultService));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string GenerateSystemSecurityKey(int keySize)
    {
        if (keySize is not (128 or 192 or 256))
            throw new ArgumentOutOfRangeException(nameof(keySize), "Key size must be 128, 192, or 256 bits.");

        var initialKey = new byte[keySize / 8];
        RandomNumberGenerator.Fill(initialKey);

        using var keyDerivation = new Rfc2898DeriveBytes(
            initialKey,
            RandomNumberGenerator.GetBytes(32),
            200000,
            HashAlgorithmName.SHA512);

        var finalKey = keyDerivation.GetBytes(keySize / 8);
        return Convert.ToBase64String(finalKey);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Task<string> EncryptAsync(string data, string key)
    {
        var nonce = new byte[Security.KeyVault.NonceSize];
        RandomNumberGenerator.Fill(nonce);

        var plaintext = Encoding.UTF8.GetBytes(data);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[Security.KeyVault.TagSize];

        using var aesGcm = new AesGcm(Convert.FromBase64String(key), Security.KeyVault.TagSize);
        aesGcm.Encrypt(nonce, plaintext, ciphertext, tag);

        var result = new byte[Security.KeyVault.NonceSize + ciphertext.Length +
                              Security.KeyVault.TagSize];
        Buffer.BlockCopy(nonce, 0, result, 0, Security.KeyVault.NonceSize);
        Buffer.BlockCopy(ciphertext, 0, result, Security.KeyVault.NonceSize, ciphertext.Length);
        Buffer.BlockCopy(tag, 0, result, Security.KeyVault.NonceSize + ciphertext.Length,
            Security.KeyVault.TagSize);

        var output = Convert.ToBase64String(result);

        byte[][] buffers = [plaintext, ciphertext, result];
        CleanMemory(buffers.AsSpan());

        return Task.FromResult(output);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Task<string> DecryptAsync(string encryptedData, string key)
    {
        var fullData = Convert.FromBase64String(encryptedData);
        if (fullData.Length < Security.KeyVault.NonceSize + Security.KeyVault.TagSize)
            throw new ArgumentException("Encrypted data is invalid or corrupted");

        var nonce = new byte[Security.KeyVault.NonceSize];
        Buffer.BlockCopy(fullData, 0, nonce, 0, Security.KeyVault.NonceSize);

        var ciphertext = new byte[fullData.Length - Security.KeyVault.NonceSize -
                                  Security.KeyVault.TagSize];
        Buffer.BlockCopy(fullData, Security.KeyVault.NonceSize, ciphertext, 0, ciphertext.Length);

        var tag = new byte[Security.KeyVault.TagSize];
        Buffer.BlockCopy(fullData, fullData.Length - Security.KeyVault.TagSize, tag, 0,
            Security.KeyVault.TagSize);

        var plaintext = new byte[ciphertext.Length];

        using var aesGcm = new AesGcm(Convert.FromBase64String(key), Security.KeyVault.TagSize);
        aesGcm.Decrypt(nonce, ciphertext, tag, plaintext);

        var output = Encoding.UTF8.GetString(plaintext);

        byte[][] buffers = [fullData, ciphertext, tag, plaintext];
        CleanMemory(buffers.AsSpan());

        return Task.FromResult(output);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CleanMemory(ReadOnlySpan<byte[]> buffers)
    {
        foreach (var buffer in buffers) CryptographicOperations.ZeroMemory(buffer.AsSpan());
    }
}