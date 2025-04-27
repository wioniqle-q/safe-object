using SafeObject.Core.Interfaces;
using SafeObject.Core.Services.Provider;

namespace SafeObject.Core.Services.Factory;

public static class HashAlgorithmProviderFactory
{
    public static IHashAlgorithmProvider Instance { get; } = CreateProvider();

    private static IHashAlgorithmProvider CreateProvider()
    {
        if (OperatingSystem.IsMacOS())
            return new MacOsHashAlgorithmProvider();

        return new DefaultHashAlgorithmProvider();
    }
}