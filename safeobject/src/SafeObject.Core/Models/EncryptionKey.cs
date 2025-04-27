using SafeObject.Core.Helpers;

namespace SafeObject.Core.Models;

public sealed record EncryptionKey
{
    public EncryptionKey(string fileId, byte[] encryptedFilePrivateKey)
    {
        ValidateParameters([
            (nameof(fileId), fileId, ValidateString, Constants.EncryptionKey.FileIdNullErrorMessage),
            (nameof(encryptedFilePrivateKey), encryptedFilePrivateKey, ValidateByteArray,
                Constants.EncryptionKey.EncryptedFilePrivateKeyNullErrorMessage)
        ]);
        FileId = fileId;
        EncryptedFilePrivateKey = encryptedFilePrivateKey.AsMemory();
    }

    public string FileId { get; }
    public ReadOnlyMemory<byte> EncryptedFilePrivateKey { get; }

    private static void ValidateParameters(
        (string Name, object Value, Func<object, bool> Validator, string ErrorMessage)[] parameters)
    {
        foreach (var (name, value, validator, errorMessage) in parameters)
            if (validator(value) is not true)
                throw new ArgumentException(errorMessage, name);
    }

    private static bool ValidateString(object value)
    {
        if (value is not string str)
            return false;
        var span = str.AsSpan();
        return span.IsEmpty is not true && span.IsWhiteSpace() is not true;
    }

    private static bool ValidateByteArray(object value)
    {
        if (value is not byte[] bytes)
            return false;
        return bytes.Length > 0;
    }
}