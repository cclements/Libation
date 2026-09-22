using FileManager;
using System;
using System.IO;
using System.Text;

namespace AaxDecrypter;

internal static class KeySidecar
{
    // Key sidecars intentionally retain the existing portable plaintext format.
    // Restrict Unix access at creation, before any key bytes reach the filesystem.
    internal static void WriteAllText(string path, string contents)
    {
        byte[] bytes = new UTF8Encoding(false, true).GetBytes(contents);
        string destination = Path.GetFullPath(path);
        string staged = Path.Combine(Path.GetDirectoryName(destination)!, $".libation-key-{Guid.NewGuid():N}.tmp");
        var options = new FileStreamOptions
        {
            Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None
        };
        if (!OperatingSystem.IsWindows())
            options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        try
        {
            using (var output = new FileStream(staged, options))
            {
                output.Write(bytes);
                output.Flush(flushToDisk: true);
            }
            // Replace the directory entry only after the complete sidecar is ready.
            // In particular, never follow an existing destination symlink for writing.
            File.Move(staged, destination, overwrite: true);
        }
        finally
        {
            // Never remove the prior sidecar or mask the original write failure.
            FileUtility.TrySaferDelete(staged);
        }
    }
}
