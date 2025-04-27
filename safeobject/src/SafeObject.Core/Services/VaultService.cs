using System.Buffers;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using SafeObject.Core.Interfaces;
using SafeObject.Core.Services.Factory;
using static SafeObject.Core.Helpers.Constants;
using EncryptionKey = SafeObject.Core.Models.EncryptionKey;

namespace SafeObject.Core.Services;

// You might want to add your own vault service implementation
public sealed class VaultService : IVaultService, IDisposable
{
    private static readonly ArrayPool<byte> SecurePool = ArrayPool<byte>.Create();

    private readonly ConcurrentDictionary<string, EncryptionKey> _keyStore = new();
    private readonly byte[] _systemSecurityKey = Convert.FromBase64String(GenerateSystemSecurityKey(256));
    private volatile bool _disposed;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public async Task<byte[]> StoreKeyAsync(string fileId, string filePrivateKey, string filePublicMasterKey)
    {
        ThrowIfDisposed();

        var encryptedPrivateKey = await EncryptAsync(filePrivateKey, filePublicMasterKey);
        var finalEncryptedKey = await EncryptAsync(encryptedPrivateKey, _systemSecurityKey);

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

        var decryptedLayerOne =
            await DecryptAsync(encryptionKey.EncryptedFilePrivateKey.Span.ToArray(), _systemSecurityKey);
        var decryptedLayerTwo = await DecryptAsync(decryptedLayerOne, Convert.FromBase64String(filePublicMasterKey));

        return Encoding.UTF8.GetString(decryptedLayerTwo);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            _keyStore.Clear();
            SecurePool.Return(_systemSecurityKey, true);
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

        var initialKey = SecurePool.Rent(keySize / 8);
        var salt = SecurePool.Rent(32);
        var finalKey = SecurePool.Rent(keySize / 8);
        
        try
        {
            RandomNumberGenerator.Fill(initialKey.AsSpan(0, keySize / 8));
            RandomNumberGenerator.Fill(salt.AsSpan(0, 32));

            var hashAlgorithm = HashAlgorithmProviderFactory.Instance.GetHashAlgorithmName();

            using var keyDerivation = new Rfc2898DeriveBytes(
                initialKey.AsSpan(0, keySize / 8).ToArray(),
                salt.AsSpan(0, 32).ToArray(),
                100000,
                hashAlgorithm);

            keyDerivation.GetBytes(keySize / 8).CopyTo(finalKey.AsSpan(0, keySize / 8));
            return Convert.ToBase64String(finalKey.AsSpan(0, keySize / 8));
        }
        finally
        {
            SecurePool.Return(initialKey, true);
            SecurePool.Return(salt, true);
            SecurePool.Return(finalKey, true);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Task<byte[]> EncryptAsync(string data, string key)
    {
        var plaintext = Encoding.UTF8.GetBytes(data);
        var keyBytes = Convert.FromBase64String(key);

        var nonce = SecurePool.Rent(Security.KeyVault.NonceSize);
        var ciphertext = SecurePool.Rent(plaintext.Length);
        var tag = SecurePool.Rent(Security.KeyVault.TagSize);
        var result = SecurePool.Rent(Security.KeyVault.NonceSize + plaintext.Length + Security.KeyVault.TagSize);

        try
        {
            RandomNumberGenerator.Fill(nonce.AsSpan(0, Security.KeyVault.NonceSize));

            using var aesGcm = new AesGcm(keyBytes, Security.KeyVault.TagSize);
            aesGcm.Encrypt(
                nonce.AsSpan(0, Security.KeyVault.NonceSize),
                plaintext.AsSpan(),
                ciphertext.AsSpan(0, plaintext.Length),
                tag.AsSpan(0, Security.KeyVault.TagSize));

            Buffer.BlockCopy(nonce, 0, result, 0, Security.KeyVault.NonceSize);
            Buffer.BlockCopy(ciphertext, 0, result, Security.KeyVault.NonceSize, plaintext.Length);
            Buffer.BlockCopy(tag, 0, result, Security.KeyVault.NonceSize + plaintext.Length, Security.KeyVault.TagSize);

            var output = new byte[Security.KeyVault.NonceSize + plaintext.Length + Security.KeyVault.TagSize];
            result.AsSpan(0, output.Length).CopyTo(output.AsSpan());

            return Task.FromResult(output);
        }
        finally
        {
            SecurePool.Return(nonce, true);
            SecurePool.Return(ciphertext, true);
            SecurePool.Return(tag, true);
            SecurePool.Return(result, true);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Task<byte[]> EncryptAsync(byte[] data, byte[] key)
    {
        var nonce = SecurePool.Rent(Security.KeyVault.NonceSize);
        var ciphertext = SecurePool.Rent(data.Length);
        var tag = SecurePool.Rent(Security.KeyVault.TagSize);
        var result = SecurePool.Rent(Security.KeyVault.NonceSize + data.Length + Security.KeyVault.TagSize);

        try
        {
            RandomNumberGenerator.Fill(nonce.AsSpan(0, Security.KeyVault.NonceSize));

            using var aesGcm = new AesGcm(key, Security.KeyVault.TagSize);
            aesGcm.Encrypt(
                nonce.AsSpan(0, Security.KeyVault.NonceSize),
                data.AsSpan(),
                ciphertext.AsSpan(0, data.Length),
                tag.AsSpan(0, Security.KeyVault.TagSize));

            Buffer.BlockCopy(nonce, 0, result, 0, Security.KeyVault.NonceSize);
            Buffer.BlockCopy(ciphertext, 0, result, Security.KeyVault.NonceSize, data.Length);
            Buffer.BlockCopy(tag, 0, result, Security.KeyVault.NonceSize + data.Length, Security.KeyVault.TagSize);

            var output = new byte[Security.KeyVault.NonceSize + data.Length + Security.KeyVault.TagSize];
            result.AsSpan(0, output.Length).CopyTo(output.AsSpan());

            return Task.FromResult(output);
        }
        finally
        {
            SecurePool.Return(nonce, true);
            SecurePool.Return(ciphertext, true);
            SecurePool.Return(tag, true);
            SecurePool.Return(result, true);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Task<byte[]> DecryptAsync(byte[] encryptedData, byte[] key)
    {
        if (encryptedData.Length < Security.KeyVault.NonceSize + Security.KeyVault.TagSize)
            throw new ArgumentException("Encrypted data is invalid or corrupted", nameof(encryptedData));

        var expectedLength = encryptedData.Length - Security.KeyVault.NonceSize - Security.KeyVault.TagSize;
        if (expectedLength <= 0)
            throw new ArgumentException("Encrypted data does not contain valid ciphertext", nameof(encryptedData));

        var nonce = SecurePool.Rent(Security.KeyVault.NonceSize);
        var ciphertext = SecurePool.Rent(expectedLength);
        var tag = SecurePool.Rent(Security.KeyVault.TagSize);
        var plaintext = SecurePool.Rent(expectedLength);

        try
        {
            Buffer.BlockCopy(encryptedData, 0, nonce, 0, Security.KeyVault.NonceSize);
            Buffer.BlockCopy(encryptedData, Security.KeyVault.NonceSize, ciphertext, 0, expectedLength);
            Buffer.BlockCopy(encryptedData, Security.KeyVault.NonceSize + expectedLength, tag, 0,
                Security.KeyVault.TagSize);

            using var aesGcm = new AesGcm(key, Security.KeyVault.TagSize);
            aesGcm.Decrypt(
                nonce.AsSpan(0, Security.KeyVault.NonceSize),
                ciphertext.AsSpan(0, expectedLength),
                tag.AsSpan(0, Security.KeyVault.TagSize),
                plaintext.AsSpan(0, expectedLength));

            var output = new byte[expectedLength];
            plaintext.AsSpan(0, output.Length).CopyTo(output.AsSpan());

            return Task.FromResult(output);
        }
        finally
        {
            SecurePool.Return(nonce, true);
            SecurePool.Return(ciphertext, true);
            SecurePool.Return(tag, true);
            SecurePool.Return(plaintext, true);
        }
    }
}