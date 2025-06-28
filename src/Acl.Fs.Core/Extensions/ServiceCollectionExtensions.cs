using Acl.Fs.Core.Factories;
using Acl.Fs.Core.Interfaces;
using Acl.Fs.Core.Interfaces.Decryption.AesGcm;
using Acl.Fs.Core.Interfaces.Decryption.ChaCha20Poly1305;
using Acl.Fs.Core.Interfaces.Encryption.AesGcm;
using Acl.Fs.Core.Interfaces.Encryption.ChaCha20Poly1305;
using Acl.Fs.Core.Interfaces.Factory;
using Acl.Fs.Core.Services.Decryption.AesGcm;
using Acl.Fs.Core.Services.Decryption.ChaCha20Poly1305;
using Acl.Fs.Core.Services.Encryption.AesGcm;
using Acl.Fs.Core.Services.Encryption.ChaCha20Poly1305;
using Acl.Fs.Core.Versioning;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Acl.Fs.Core.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAclFsCore(this IServiceCollection services)
    {
        services.TryAddSingleton<IFileVersionValidator, FileVersionValidator>();

        return services;
    }

    public static IServiceCollection AddAesGcmServices(this IServiceCollection services)
    {
        services.TryAddSingleton<IAesGcmFactory, AesGcmFactory>();

        services.TryAddScoped<IAesEncryptionBase, AesEncryptionBase>();
        services.TryAddScoped<IAesDecryptionBase, AesDecryptionBase>();
        services.TryAddScoped<IAesEncryptionService, AesEncryptionService>();
        services.TryAddScoped<IAesDecryptionService, AesDecryptionService>();

        return services;
    }

    public static IServiceCollection AddChaCha20Poly1305Services(this IServiceCollection services)
    {
        services.TryAddSingleton<IChaCha20Poly1305Factory, ChaCha20Poly1305Factory>();

        services.TryAddScoped<IChaCha20Poly1305EncryptionBase, ChaCha20Poly1305EncryptionBase>();
        services.TryAddScoped<IChaCha20Poly1305DecryptionBase, ChaCha20Poly1305DecryptionBase>();
        services.TryAddScoped<IChaCha20Poly1305EncryptionService, ChaCha20Poly1305EncryptionService>();
        services.TryAddScoped<IChaCha20Poly1305DecryptionService, ChaCha20Poly1305DecryptionService>();

        return services;
    }
}