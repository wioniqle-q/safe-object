using Acl.Fs.Core.Factories;
using Acl.Fs.Core.Interfaces;
using Acl.Fs.Core.Interfaces.Decryption.AesGcm;
using Acl.Fs.Core.Interfaces.Encryption.AesGcm;
using Acl.Fs.Core.Interfaces.Factory;
using Acl.Fs.Core.Services.Decryption.AesGcm;
using Acl.Fs.Core.Services.Encryption.AesGcm;
using Acl.Fs.Core.Versioning;
using Acl.Fs.Core.Versioning.ValidationStrategies;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Acl.Fs.Core.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAclFsCore(this IServiceCollection services)
    {
        services.TryAddSingleton<IAesGcmFactory, AesGcmFactory>();
        services.TryAddSingleton<IFileVersionValidator, FileVersionValidator>();

        services.TryAddScoped<IAesEncryptionBase, AesEncryptionBase>();
        services.TryAddScoped<IAesDecryptionBase, AesDecryptionBase>();

        services.TryAddScoped<IAesEncryptionService, AesEncryptionService>();
        services.TryAddScoped<IAesDecryptionService, AesDecryptionService>();
        services.TryAddScoped<IVersionValidationStrategy, V1ValidationStrategy>();

        return services;
    }
}