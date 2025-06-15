using System.Security.Cryptography;

namespace Acl.Fs.Core.Interfaces.Factory;

internal interface IAesGcmFactory
{
    AesGcm Create(byte[] key);
}