using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using safe_object.Interfaces;
using safe_object.Models;
using Sodium;
using static safe_object.Constants;

namespace safe_object.Services;

public sealed class VaultService : IVaultService, IDisposable
{
    private readonly ConcurrentDictionary<string, EncryptionKey> _keyStore = new();
    private readonly byte[] _systemSecurityKey = GenerateSystemSecurityKey(Security.KeyVault.KeySize);
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

        var encryptedPrivateKey = await EncryptAsync(filePrivateKey, Convert.FromBase64String(filePublicMasterKey))
            .ConfigureAwait(false);
        var finalEncryptedKey = await EncryptAsync(encryptedPrivateKey, _systemSecurityKey)
            .ConfigureAwait(false);

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

        return await DecryptAsync(decryptedLayerOne, Convert.FromBase64String(filePublicMasterKey))
            .ConfigureAwait(false);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing) _keyStore.Clear();

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
    private static byte[] GenerateSystemSecurityKey(int keySizeBytes)
    {
        if (keySizeBytes is not 32)
            throw new ArgumentOutOfRangeException(nameof(keySizeBytes),
                "Key size must be 32 bytes (256 bits) for XChaCha20-Poly1305.");

        return SodiumCore.GetRandomBytes(keySizeBytes);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Task<string> EncryptAsync(string data, byte[] key)
    {
        var plaintext = Encoding.UTF8.GetBytes(data);

        var nonce = SecretAeadXChaCha20Poly1305.GenerateNonce();

        var ciphertext = SecretAeadXChaCha20Poly1305.Encrypt(
            plaintext,
            nonce,
            key);

        var result = new byte[nonce.Length + ciphertext.Length];
        Buffer.BlockCopy(nonce, 0, result, 0, nonce.Length);
        Buffer.BlockCopy(ciphertext, 0, result, nonce.Length, ciphertext.Length);

        var output = Convert.ToBase64String(result);

        var buffers = new[]
        {
            plaintext,
            nonce,
            ciphertext
        };
        CleanMemory(buffers);

        return Task.FromResult(output);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Task<string> DecryptAsync(string encryptedData, byte[] key)
    {
        var combined = Convert.FromBase64String(encryptedData);

        var nonce = new byte[Security.KeyVault.NonceSize];
        var ciphertext = new byte[combined.Length - nonce.Length];

        Buffer.BlockCopy(combined, 0, nonce, 0, nonce.Length);
        Buffer.BlockCopy(combined, nonce.Length, ciphertext, 0, ciphertext.Length);

        var plaintext = SecretAeadXChaCha20Poly1305.Decrypt(
            ciphertext,
            nonce,
            key);

        var output = Encoding.UTF8.GetString(plaintext);

        var buffers = new[]
        {
            nonce,
            ciphertext,
            plaintext
        };
        CleanMemory(buffers);

        return Task.FromResult(output);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CleanMemory(ReadOnlySpan<byte[]> buffers)
    {
        foreach (var buffer in buffers) CryptographicOperations.ZeroMemory(buffer.AsSpan());
    }
}