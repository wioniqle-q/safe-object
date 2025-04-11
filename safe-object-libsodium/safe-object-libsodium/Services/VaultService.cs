using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using safe_object.Interfaces;
using safe_object.Models;
using Sodium;

namespace safe_object.Services;

public sealed class VaultService : IVaultService, IDisposable
{
    private readonly ConcurrentDictionary<string, EncryptionKey> _keyStore = new();
    private readonly byte[] _systemSecurityKey = SecretBox.GenerateKey();
    private bool _disposed;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<string> StoreKeyAsync(string fileId, string filePrivateKey, string filePublicMasterKey)
    {
        ThrowIfDisposed();

        var decodedPrivateKey = Convert.FromBase64String(filePrivateKey);
        var decodedMasterKey = Convert.FromBase64String(filePublicMasterKey);

        var privateKeyEncryptionNonce = SecretBox.GenerateNonce();
        var systemEncryptionNonce = SecretBox.GenerateNonce();

        var masterEncryptedPrivateKey =
            SecretBox.Create(decodedPrivateKey, privateKeyEncryptionNonce, decodedMasterKey);

        var systemEncryptedKey = SecretBox.Create(masterEncryptedPrivateKey, systemEncryptionNonce, _systemSecurityKey);

        var encryptedKeyBundle = new byte[privateKeyEncryptionNonce.Length + systemEncryptionNonce.Length +
                                          systemEncryptedKey.Length];
        Buffer.BlockCopy(privateKeyEncryptionNonce, 0, encryptedKeyBundle, 0, privateKeyEncryptionNonce.Length);
        Buffer.BlockCopy(systemEncryptionNonce, 0, encryptedKeyBundle, privateKeyEncryptionNonce.Length,
            systemEncryptionNonce.Length);
        Buffer.BlockCopy(systemEncryptedKey, 0, encryptedKeyBundle,
            privateKeyEncryptionNonce.Length + systemEncryptionNonce.Length,
            systemEncryptedKey.Length);

        var encryptionKey = new EncryptionKey(fileId, Convert.ToBase64String(encryptedKeyBundle));

        if (_keyStore.TryAdd(fileId, encryptionKey) is not true)
            throw new InvalidOperationException($"Key for file ID {fileId} already exists.");

        return Task.FromResult(Convert.ToBase64String(systemEncryptedKey));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<string> RetrieveKeyAsync(string fileId, string filePublicMasterKey)
    {
        ThrowIfDisposed();

        if (_keyStore.TryGetValue(fileId, out var encryptionKey) is not true)
            throw new KeyNotFoundException($"No key found for file ID: {fileId}");

        var encryptedKeyBundle = Convert.FromBase64String(encryptionKey.EncryptedFilePrivateKey);
        var decodedMasterKey = Convert.FromBase64String(filePublicMasterKey);

        var systemEncryptedData = encryptedKeyBundle.Skip(48).ToArray();
        var systemEncryptionNonce = encryptedKeyBundle.Skip(24).Take(24).ToArray();
        var privateKeyEncryptionNonce = encryptedKeyBundle.Take(24).ToArray();

        var masterEncryptedPrivateKey = SecretBox.Open(systemEncryptedData, systemEncryptionNonce, _systemSecurityKey);
        if (masterEncryptedPrivateKey is null)
            throw new CryptographicException("Failed to decrypt with system security key.");

        var decryptedPrivateKey =
            SecretBox.Open(masterEncryptedPrivateKey, privateKeyEncryptionNonce, decodedMasterKey);
        if (decryptedPrivateKey is null)
            throw new CryptographicException("Failed to decrypt with master key.");

        return Task.FromResult(Convert.ToBase64String(decryptedPrivateKey));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Dispose(bool disposing)
    {
        if (_disposed) return;
        if (disposing)
        {
            _keyStore.Clear();
            CryptographicOperations.ZeroMemory(_systemSecurityKey);
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
}