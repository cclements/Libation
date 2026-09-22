namespace AaxDecrypter;

internal static class KeySidecar
{
    // Keep the portable plaintext format; restrict access before any key bytes are written.
    internal static void WriteAllText(string path, string contents)
        => PrivateFileWriter.WriteAllText(path, contents);
}
