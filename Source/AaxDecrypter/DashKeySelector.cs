using System;
using System.Linq;

namespace AaxDecrypter;

internal static class DashKeySelector
{
    internal static (byte[] KeyId, byte[] Key) Select(KeyData[] keys, Guid defaultKeyId)
    {
        ArgumentNullException.ThrowIfNull(keys);
        if (keys.Length == 0)
            throw new InvalidOperationException("DecryptionKeys cannot be empty for a DASH file.");
        var keyIds = keys.Select(k => new Guid(k.KeyPart1, bigEndian: true)).ToArray();
        int index = Array.IndexOf(keyIds, defaultKeyId);
        if (index == -1)
            throw new InvalidOperationException($"None of the {keyIds.Length} key IDs match the dash file's default KeyID of {defaultKeyId}");
        var selected = keys[index];
        var key = selected.KeyPart2
            ?? throw new InvalidOperationException("DecryptionKeys for 'Dash' must have a non-null decryption key (KeyPart2).");
        return (selected.KeyPart1, key);
    }
}
