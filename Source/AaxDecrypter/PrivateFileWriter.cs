using FileManager;
using System;
using System.IO;
using System.Text;

namespace AaxDecrypter;

/// <summary>Publish sensitive text only after a private, flushed staging file is complete.</summary>
internal static class PrivateFileWriter
{
    internal static void WriteAllText(string path, string contents, Action<string>? validate = null)
    {
        byte[] bytes = new UTF8Encoding(false, true).GetBytes(contents);
        string destination = Path.GetFullPath(path);
        string staged = Path.Combine(Path.GetDirectoryName(destination)!, $".libation-private-{Guid.NewGuid():N}.tmp");
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
            validate?.Invoke(staged);
            // Replacing the directory entry never follows a destination symlink for writing.
            File.Move(staged, destination, overwrite: true);
        }
        finally
        {
            // Never remove the prior file or mask the original write/validation failure.
            FileUtility.TrySaferDelete(staged);
        }
    }
}
