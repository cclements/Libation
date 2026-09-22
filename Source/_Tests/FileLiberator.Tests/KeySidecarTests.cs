using AaxDecrypter;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Text;

namespace FileLiberator.Tests;

[TestClass]
public class KeySidecarTests
{
    private string directory = null!;
    private string path = null!;
    private const string Old = "synthetic previous key-sidecar contents";
    private const string New = "Key=synthetic-fixture-only\nIV=synthetic-fixture-only\n";
    [TestInitialize]
    public void Setup()
    {
        directory = Path.Combine(Path.GetTempPath(), "libation-key-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory); path = Path.Combine(directory, "fixture.key");
    }
    [TestCleanup] public void Cleanup() => Directory.Delete(directory, true);

    [TestMethod]
    public void NewSidecarPreservesTextAndHasOwnerOnlyUnixMode()
    {
        KeySidecar.WriteAllText(path, New);
        CollectionAssert.AreEqual(new UTF8Encoding(false).GetBytes(New), File.ReadAllBytes(path));
        AssertOwnerMode();
        Assert.AreEqual(1, Directory.GetFiles(directory).Length);
    }

    [TestMethod]
    public void ReplacingLegacySidecarRestrictsItsModeAndWritesOnlyNewContents()
    {
        File.WriteAllText(path, Old + new string('x', 2000));
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead);
        KeySidecar.WriteAllText(path, New);
        Assert.AreEqual(New, File.ReadAllText(path));
        AssertOwnerMode();
        Assert.AreEqual(1, Directory.GetFiles(directory).Length);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void FailedEncodingNeverPublishesPartialSidecar(bool hasPrevious)
    {
        if (hasPrevious) File.WriteAllText(path, Old);
        Assert.ThrowsExactly<EncoderFallbackException>(() => KeySidecar.WriteAllText(path, "synthetic prefix\uD800"));
        if (hasPrevious) Assert.AreEqual(Old, File.ReadAllText(path));
        else Assert.IsFalse(File.Exists(path));
        Assert.AreEqual(hasPrevious ? 1 : 0, Directory.GetFiles(directory).Length);
    }

    [TestMethod]
    public void DestinationSymlinkDoesNotRedirectKeyWrite()
    {
        if (OperatingSystem.IsWindows()) { Assert.Inconclusive("Unix symlink behavior; Windows link/ACL proof is separate."); return; }
        string target = Path.Combine(directory, "unrelated.txt");
        File.WriteAllText(target, Old);
        File.CreateSymbolicLink(path, target);
        KeySidecar.WriteAllText(path, New);
        Assert.AreEqual(Old, File.ReadAllText(target));
        Assert.IsNull(new FileInfo(path).LinkTarget);
        Assert.AreEqual(New, File.ReadAllText(path));
        AssertOwnerMode();
        Assert.AreEqual(2, Directory.GetFiles(directory).Length);
    }

    [TestMethod]
    public void PublicationFailurePreservesDestinationDirectoryAndCleansStaging()
    {
        Directory.CreateDirectory(path);
        Exception? failure = null;
        try { KeySidecar.WriteAllText(path, New); }
        catch (Exception ex) { failure = ex; }
        Assert.IsTrue(failure is IOException or UnauthorizedAccessException);
        Assert.IsTrue(Directory.Exists(path));
        Assert.AreEqual(0, Directory.GetFiles(directory).Length);
    }

    [TestMethod]
    public void MissingParentDoesNotCreateUntrackedFiles()
    {
        Assert.ThrowsExactly<DirectoryNotFoundException>(() => KeySidecar.WriteAllText(Path.Combine(directory, "missing", "fixture.key"), New));
        Assert.AreEqual(0, Directory.GetFileSystemEntries(directory).Length);
    }

    private void AssertOwnerMode()
    {
        if (!OperatingSystem.IsWindows())
            Assert.AreEqual(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(path));
    }
}
